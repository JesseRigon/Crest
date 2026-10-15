using System.Text.Json;
using System.Text.Json.Nodes;
using Grove.Api;
using Grove.Json;
using Grove.Seo.Sitemaps.Cache;
using Grove.Seo.Sitemaps.Models;
using Grove.Seo.Sitemaps.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace Grove.Seo.Sitemaps.Api;

/// <summary>
/// <c>api/grove/sitemaps</c>: sitemaps, sitemap indexes, their sources and the sitemap cache, for
/// the sitemaps pages (Grove.Seo.Sitemaps.BlazorWasm). Every endpoint needs <c>ManageSitemaps</c>.
/// </summary>
public sealed class SitemapsApi : ApiContributor
{
    public override string Name => "Sitemaps";

    public override string FeatureId => "Grove.Seo.Sitemaps";

    public override void Map(IEndpointRouteBuilder group)
    {
        var routes = group.MapGroup("sitemaps");
        routes.MapGet("", ApiHandlers.Handle<SitemapsHandlers>(h => h.ListAsync()));
        routes.MapPost("", (HttpContext http, [FromBody] SitemapWrite request) => ApiHandlers.For<SitemapsHandlers>(http).CreateAsync(request));
        routes.MapGet("source-types", ApiHandlers.Handle<SitemapsHandlers>(h => h.ListSourceTypesAsync()));
        routes.MapGet("content-types", ApiHandlers.Handle<SitemapsHandlers>(h => h.ListContentTypesAsync()));
        routes.MapGet("cache", ApiHandlers.Handle<SitemapsHandlers>(h => h.ListCacheAsync()));
        routes.MapDelete("cache", ApiHandlers.Handle<SitemapsHandlers>(h => h.PurgeCacheAsync()));
        routes.MapDelete("cache/{fileName}", (HttpContext http, [FromRoute] string fileName) => ApiHandlers.For<SitemapsHandlers>(http).PurgeCacheFileAsync(fileName));
        routes.MapGet("{sitemapId}", (HttpContext http, [FromRoute] string sitemapId) => ApiHandlers.For<SitemapsHandlers>(http).GetAsync(sitemapId));
        routes.MapPut("{sitemapId}", (HttpContext http, [FromRoute] string sitemapId, [FromBody] SitemapWrite request) => ApiHandlers.For<SitemapsHandlers>(http).UpdateAsync(sitemapId, request));
        routes.MapDelete("{sitemapId}", (HttpContext http, [FromRoute] string sitemapId) => ApiHandlers.For<SitemapsHandlers>(http).DeleteAsync(sitemapId));
        routes.MapPost("{sitemapId}/sources", (HttpContext http, [FromRoute] string sitemapId, [FromBody] SitemapSourceWrite request) => ApiHandlers.For<SitemapsHandlers>(http).CreateSourceAsync(sitemapId, request));
        routes.MapPut("{sitemapId}/sources/{sourceId}", (HttpContext http, [FromRoute] string sitemapId, [FromRoute] string sourceId, [FromBody] SitemapSourceWrite request) => ApiHandlers.For<SitemapsHandlers>(http).UpdateSourceAsync(sitemapId, sourceId, request));
        routes.MapDelete("{sitemapId}/sources/{sourceId}", (HttpContext http, [FromRoute] string sitemapId, [FromRoute] string sourceId) => ApiHandlers.For<SitemapsHandlers>(http).DeleteSourceAsync(sitemapId, sourceId));
    }
}

public sealed class SitemapsHandlers(
    IAuthorizationService authorization,
    ISitemapManager sitemaps,
    ISitemapHelperService helper,
    ISitemapIdGenerator ids,
    IEnumerable<ISitemapSourceFactory> sourceFactories,
    IRouteableContentTypeCoordinator routableTypes,
    ISitemapCacheProvider cache) : ApiHandlers
{
    public async Task<IResult> ListAsync()
    {
        if (!await CanManageAsync()) return Forbid();

        return Ok((await sitemaps.GetSitemapsAsync())
            .OrderBy(sitemap => sitemap.Name, StringComparer.OrdinalIgnoreCase)
            .Select(sitemap => ToModel(sitemap, withSources: false))
            .ToArray());
    }

    public async Task<IResult> GetAsync(string sitemapId)
    {
        if (!await CanManageAsync()) return Forbid();

        var sitemap = await sitemaps.GetSitemapAsync(sitemapId);
        return sitemap is null ? NotFound() : Ok(ToModel(sitemap, withSources: true));
    }

    public async Task<IResult> CreateAsync(SitemapWrite request)
    {
        if (!await CanManageAsync()) return Forbid();

        SitemapType sitemap = request.Kind == SitemapKinds.Index ? new SitemapIndex() : new Sitemap();
        sitemap.SitemapId = ids.GenerateUniqueId();
        if (sitemap is SitemapIndex)
        {
            sitemap.SitemapSources.Add(new SitemapIndexSource { Id = ids.GenerateUniqueId() });
        }

        return await SaveAsync(sitemap, request, created: true);
    }

    public async Task<IResult> UpdateAsync(string sitemapId, SitemapWrite request)
    {
        if (!await CanManageAsync()) return Forbid();

        var sitemap = await sitemaps.LoadSitemapAsync(sitemapId);
        return sitemap is null ? NotFound() : await SaveAsync(sitemap, request, created: false);
    }

    public async Task<IResult> DeleteAsync(string sitemapId)
    {
        if (!await CanManageAsync()) return Forbid();

        if (await sitemaps.LoadSitemapAsync(sitemapId) is null)
        {
            return NotFound();
        }

        await sitemaps.DeleteSitemapAsync(sitemapId);
        return NoContent();
    }

    public async Task<IResult> ListSourceTypesAsync()
    {
        if (!await CanManageAsync()) return Forbid();

        return Ok(sourceFactories.Select(factory => factory.Name).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray());
    }

    public async Task<IResult> ListContentTypesAsync()
    {
        if (!await CanManageAsync()) return Forbid();

        return Ok((await routableTypes.ListRoutableTypeDefinitionsAsync())
            .Select(type => new RoutableContentTypeModel(type.Name, type.DisplayName))
            .OrderBy(type => type.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ToArray());
    }

    public async Task<IResult> CreateSourceAsync(string sitemapId, SitemapSourceWrite request)
    {
        if (!await CanManageAsync()) return Forbid();

        if (await sitemaps.LoadSitemapAsync(sitemapId) is not Sitemap sitemap)
        {
            return NotFound();
        }

        var factory = sourceFactories.FirstOrDefault(candidate => candidate.Name == request.Type);
        if (factory is null)
        {
            return ApiProblems.Validation("type", $"There is no sitemap source type named '{request.Type}'.");
        }

        var template = factory.Create();
        var source = Read(request.Data, template.GetType(), template.Id);
        var errors = Validate(source);
        if (errors.Count > 0)
        {
            return ApiProblems.Validation(errors);
        }

        sitemap.SitemapSources.Add(source);
        await sitemaps.UpdateSitemapAsync(sitemap);
        return Ok(ToModel(source));
    }

    public async Task<IResult> UpdateSourceAsync(string sitemapId, string sourceId, SitemapSourceWrite request)
    {
        if (!await CanManageAsync()) return Forbid();

        if (await sitemaps.LoadSitemapAsync(sitemapId) is not Sitemap sitemap)
        {
            return NotFound();
        }

        var index = sitemap.SitemapSources.FindIndex(candidate => string.Equals(candidate.Id, sourceId, StringComparison.OrdinalIgnoreCase));
        if (index < 0)
        {
            return NotFound();
        }

        var source = Read(request.Data, sitemap.SitemapSources[index].GetType(), sitemap.SitemapSources[index].Id);
        var errors = Validate(source);
        if (errors.Count > 0)
        {
            return ApiProblems.Validation(errors);
        }

        sitemap.SitemapSources[index] = source;
        await sitemaps.UpdateSitemapAsync(sitemap);
        return Ok(ToModel(source));
    }

    public async Task<IResult> DeleteSourceAsync(string sitemapId, string sourceId)
    {
        if (!await CanManageAsync()) return Forbid();

        if (await sitemaps.LoadSitemapAsync(sitemapId) is not Sitemap sitemap)
        {
            return NotFound();
        }

        if (sitemap.SitemapSources.RemoveAll(candidate => string.Equals(candidate.Id, sourceId, StringComparison.OrdinalIgnoreCase)) == 0)
        {
            return NotFound();
        }

        await sitemaps.UpdateSitemapAsync(sitemap);
        return NoContent();
    }

    public async Task<IResult> ListCacheAsync()
    {
        if (!await CanManageAsync()) return Forbid();

        return Ok((await cache.ListAsync()).Order(StringComparer.OrdinalIgnoreCase).Select(fileName => new SitemapCacheFileModel(fileName)).ToArray());
    }

    public async Task<IResult> PurgeCacheAsync()
    {
        if (!await CanManageAsync()) return Forbid();

        return await cache.PurgeAllAsync()
            ? ApiProblems.Conflict("The sitemap cache was purged, with errors.")
            : NoContent();
    }

    public async Task<IResult> PurgeCacheFileAsync(string fileName)
    {
        if (!await CanManageAsync()) return Forbid();

        return await cache.PurgeAsync(fileName)
            ? ApiProblems.Conflict($"The cached sitemap '{fileName}' could not be purged.")
            : NoContent();
    }

    private async Task<IResult> SaveAsync(SitemapType sitemap, SitemapWrite request, bool created)
    {
        var path = string.IsNullOrWhiteSpace(request.Path) ? helper.GetSitemapSlug(request.Name ?? string.Empty) : request.Path.Trim();
        var errors = new Grove.Validation.ValidationErrors();
        if (string.IsNullOrWhiteSpace(request.Name))
        {
            errors.Add("name", "The name is required.");
        }

        foreach (var error in await helper.ValidatePathAsync(path, created ? null : sitemap.SitemapId))
        {
            errors.Add("path", error);
        }

        if (errors.Count > 0)
        {
            return ApiProblems.Validation(errors);
        }

        sitemap.Name = request.Name!.Trim();
        sitemap.Path = path;
        sitemap.Enabled = request.Enabled;

        if (sitemap is SitemapIndex)
        {
            var known = (await sitemaps.GetSitemapsAsync()).OfType<Sitemap>().Select(candidate => candidate.SitemapId).ToHashSet(StringComparer.Ordinal);
            var indexSource = sitemap.SitemapSources.OfType<SitemapIndexSource>().FirstOrDefault();
            if (indexSource is null)
            {
                indexSource = new SitemapIndexSource { Id = ids.GenerateUniqueId() };
                sitemap.SitemapSources.Add(indexSource);
            }

            indexSource.ContainedSitemapIds = [.. (request.ContainedSitemapIds ?? []).Where(known.Contains).Distinct(StringComparer.Ordinal)];
        }

        await sitemaps.UpdateSitemapAsync(sitemap);
        var model = ToModel(sitemap, withSources: true);
        return created ? Created(SitemapsApiPaths.Sitemap(sitemap.SitemapId), model) : Ok(model);
    }

    private static SitemapSource Read(JsonObject? data, Type type, string id)
    {
        var source = (SitemapSource)(JsonSerializer.Deserialize(data ?? [], type, JOptions.CamelCase) ?? Activator.CreateInstance(type)!);
        source.Id = id;
        if (source is CustomPathSitemapSource customPath)
        {
            customPath.Path = customPath.Path?.Trim();
            customPath.LastUpdate = DateTime.Now;
        }

        return source;
    }

    // The custom path source's rules; other sources carry no rules of their own.
    private static Grove.Validation.ValidationErrors Validate(SitemapSource source)
    {
        var errors = new Grove.Validation.ValidationErrors();
        if (source is CustomPathSitemapSource { Path: { } path })
        {
            if (path.IndexOfAny(CustomPathSitemapSource.InvalidCharactersForPath) > -1 || path.Contains(' ') || path.Contains("//"))
            {
                var invalid = string.Join(", ", CustomPathSitemapSource.InvalidCharactersForPath.Select(character => $"\"{character}\""));
                errors.Add("data.path", $"Please do not use any of the following characters in the path: {invalid}. No spaces, or consecutive slashes, are allowed (please use dashes or underscores instead).");
            }

            if (path.Length > CustomPathSitemapSource.MaxPathLength)
            {
                errors.Add("data.path", $"The path is too long. It can only be up to {CustomPathSitemapSource.MaxPathLength} characters.");
            }
        }

        return errors;
    }

    private static SitemapModel ToModel(SitemapType sitemap, bool withSources) => new(
        sitemap.SitemapId,
        sitemap is SitemapIndex ? SitemapKinds.Index : SitemapKinds.Sitemap,
        sitemap.Name,
        sitemap.Path,
        sitemap.Enabled,
        withSources && sitemap is Sitemap ? [.. sitemap.SitemapSources.Select(ToModel)] : [],
        sitemap is SitemapIndex ? sitemap.SitemapSources.OfType<SitemapIndexSource>().FirstOrDefault()?.ContainedSitemapIds ?? [] : []);

    private static SitemapSourceModel ToModel(SitemapSource source) =>
        new(source.Id, source.GetType().Name, JsonSerializer.SerializeToNode(source, source.GetType(), JOptions.CamelCase) as JsonObject ?? []);

    private Task<bool> CanManageAsync() => authorization.AuthorizeAsync(User, SitemapsPermissions.ManageSitemaps);
}
