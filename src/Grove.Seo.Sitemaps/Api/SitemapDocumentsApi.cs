using System.Collections.Concurrent;
using System.Net.Mime;
using System.Xml.Linq;
using Grove.Api;
using Grove.Environment.Shell;
using Grove.Infrastructure.Settings;
using Grove.Routing;
using Grove.Seo.Sitemaps.Builders;
using Grove.Seo.Sitemaps.Cache;
using Grove.Seo.Sitemaps.Routing;
using Grove.Seo.Sitemaps.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Logging;

namespace Grove.Seo.Sitemaps.Api;

/// <summary>
/// The sitemap documents (docs/MVC/api.md › P11): every enabled sitemap or sitemap index at its own
/// path (<c>sitemap.xml</c>, ...), anonymous <c>GET</c>, served from the sitemap cache or built
/// and cached on the first request. A path that is not an enabled sitemap's is not matched
/// (<see cref="SitemapPathRouteConstraint"/>).
/// </summary>
public sealed class SitemapDocumentsApi : ApiContributor
{
    public override string Name => "SitemapDocuments";

    public override string FeatureId => "Grove.Seo.Sitemaps";

    public override string RoutePrefix => "";

    public override void Map(IEndpointRouteBuilder group)
    {
        group.MapGet("{**sitemap:" + SitemapPathRouteConstraint.Name + "}",
                (HttpContext http, [FromRoute] string sitemap, CancellationToken cancellationToken) =>
                    ApiHandlers.For<SitemapDocumentHandlers>(http).GetAsync(sitemap, cancellationToken))
            .AllowAnonymous()
            .ExcludeFromDescription();
    }
}

public sealed class SitemapDocumentHandlers(
    SitemapEntries entries,
    ISitemapManager sitemapManager,
    ISiteService siteService,
    ISitemapBuilder sitemapBuilder,
    ISitemapCacheProvider sitemapCacheProvider,
    IShellLinkGenerator links,
    ShellSettings shellSettings,
    ILogger<SitemapDocumentHandlers> logger) : ApiHandlers
{
    private const int WarningLength = 47_185_920;
    private const int ErrorLength = 52_428_800;

    private static readonly ConcurrentDictionary<string, Lazy<Task<Stream>>> s_workers = new(StringComparer.OrdinalIgnoreCase);

    public async Task<IResult> GetAsync(string path, CancellationToken cancellationToken)
    {
        var (found, sitemapId) = await entries.TryGetSitemapIdByPathAsync(path);
        if (!found)
        {
            return NotFound();
        }

        var sitemap = await sitemapManager.GetSitemapAsync(sitemapId);
        if (sitemap == null || !sitemap.Enabled)
        {
            return NotFound();
        }

        var tenantName = shellSettings.Name;
        var fileResolver = await sitemapCacheProvider.GetCachedSitemapAsync(sitemap.CacheFileName);
        if (fileResolver != null)
        {
            // When multiple requests occur for the same sitemap it may still be building, so we
            // wait for it to complete.
            if (s_workers.TryGetValue(tenantName + sitemap.Path, out var writeTask))
            {
                await writeTask.Value;
            }

            // The file result disposes of the stream.
            var stream = await fileResolver.OpenReadStreamAsync();
            return Results.Stream(stream, MediaTypeNames.Application.Xml);
        }

        var work = await s_workers.GetOrAdd(tenantName + sitemap.Path, x => new Lazy<Task<Stream>>(async () =>
        {
            try
            {
                var siteSettings = await siteService.GetSiteSettingsAsync();

                var context = new SitemapBuilderContext()
                {
                    HostPrefix = siteSettings.BaseUrl,
                    Links = links,
                };

                var document = await sitemapBuilder.BuildAsync(sitemap, context);
                if (document == null)
                {
                    return null;
                }

                document.Declaration = new XDeclaration("1.0", "utf-8", null);

                var stream = MemoryStreamFactory.GetStream();
                await document.SaveAsync(stream, SaveOptions.None, cancellationToken);

                if (stream.Length >= ErrorLength)
                {
                    logger.LogError("Sitemap 50MB maximum length limit exceeded");
                }
                else if (stream.Length >= WarningLength)
                {
                    logger.LogWarning("Sitemap nearing 50MB length limit");
                }

                await sitemapCacheProvider.SetSitemapCacheAsync(stream, sitemap.CacheFileName, cancellationToken);

                return stream;
            }
            finally
            {
                s_workers.TryRemove(tenantName + sitemap.Path, out _);
            }
        }, LazyThreadSafetyMode.ExecutionAndPublication)).Value;

        if (work == null)
        {
            return NotFound();
        }

        work.Position = 0;

        // The stream result disposes of the stream.
        return Results.Stream(work, MediaTypeNames.Application.Xml);
    }
}
