using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;
using Grove.ContentManagement;
using Grove.ContentManagement.Handlers;
using Grove.DisplayManagement.Extensions;
using Grove.Media;
using Grove.Media.Fields;
using Grove.Media.Library;
using Grove.Mvc.Utilities;
using Grove.Seo.Models;
using Grove.Settings;

namespace Grove.Seo.Drivers;

public class SeoMetaPartHandler : ContentPartHandler<SeoMetaPart>
{
    private readonly IMediaReferenceResolver _references;
    private readonly ISiteService _siteService;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IUrlHelperFactory _urlHelperFactory;
    private readonly IContentManager _contentManager;

    public SeoMetaPartHandler(
        IMediaReferenceResolver references,
        ISiteService siteService,
        IHttpContextAccessor httpContextAccessor,
        IUrlHelperFactory urlHelperFactory,
        IContentManager contentManager
        )
    {
        _references = references;
        _siteService = siteService;
        _httpContextAccessor = httpContextAccessor;
        _urlHelperFactory = urlHelperFactory;
        _contentManager = contentManager;
    }

    public override Task GetContentItemAspectAsync(ContentItemAspectContext context, SeoMetaPart part)
    {
        return context.ForAsync<SeoAspect>(async aspect =>
        {
            aspect.Render = part.Render;

            if (!string.IsNullOrEmpty(part.PageTitle))
            {
                aspect.PageTitle = part.PageTitle;
            }

            if (!string.IsNullOrEmpty(part.MetaDescription))
            {
                aspect.MetaDescription = part.MetaDescription;
            }

            if (!string.IsNullOrEmpty(part.MetaKeywords))
            {
                aspect.MetaKeywords = part.MetaKeywords;
            }

            if (!string.IsNullOrEmpty(part.MetaRobots))
            {
                aspect.MetaRobots = part.MetaRobots;
            }

            aspect.CustomMetaTags = part.CustomMetaTags;

            var siteSettings = await _siteService.GetSiteSettingsAsync();

            // In .NET 10, create ActionContext directly instead of using obsolete IActionContextAccessor
            var httpContext = _httpContextAccessor.HttpContext;
            var actionContext = await httpContext.GetActionContextAsync();

            var urlHelper = _urlHelperFactory.GetUrlHelper(actionContext);

            if (!string.IsNullOrEmpty(part.Canonical))
            {
                aspect.Canonical = part.Canonical;
            }
            else
            {
                var contentItemMetadata = await _contentManager.PopulateAspectAsync<ContentItemMetadata>(part.ContentItem);
                var relativeUrl = urlHelper.RouteUrl(contentItemMetadata.DisplayRouteValues);
                aspect.Canonical = urlHelper.ToAbsoluteUrl(relativeUrl);
            }

            // OpenGraph
            // Placement ids first, paths for content not yet migrated (ruling F1). Crawlers never
            // carry the caller, so the URL is never signed: a private image answers 404 to them.
            aspect.OpenGraphImage = await SocialImageUrlAsync(urlHelper, part.OpenGraphImage) ?? await SocialImageUrlAsync(urlHelper, part.DefaultSocialImage) ?? aspect.OpenGraphImage;

            if (part.OpenGraphImage?.MediaTexts?.Length > 0)
            {
                aspect.OpenGraphImageAlt = part.OpenGraphImage.MediaTexts[0];
            }
            else if (part.DefaultSocialImage?.MediaTexts?.Length > 0)
            {
                aspect.OpenGraphImageAlt = part.DefaultSocialImage.MediaTexts[0];
            }

            if (!string.IsNullOrEmpty(part.OpenGraphTitle))
            {
                aspect.OpenGraphTitle = part.OpenGraphTitle;
            }
            else
            {
                aspect.OpenGraphTitle = part.PageTitle;
            }

            if (!string.IsNullOrEmpty(part.OpenGraphDescription))
            {
                aspect.OpenGraphDescription = part.OpenGraphDescription;
            }
            else
            {
                aspect.OpenGraphDescription = part.MetaDescription;
            }

            // Twitter

            aspect.TwitterImage = await SocialImageUrlAsync(urlHelper, part.TwitterImage) ?? await SocialImageUrlAsync(urlHelper, part.DefaultSocialImage) ?? aspect.TwitterImage;

            if (part.TwitterImage?.MediaTexts?.Length > 0)
            {
                aspect.TwitterImageAlt = part.TwitterImage.MediaTexts[0];
            }
            else if (part.DefaultSocialImage?.MediaTexts?.Length > 0)
            {
                aspect.TwitterImageAlt = part.DefaultSocialImage.MediaTexts[0];
            }

            if (!string.IsNullOrEmpty(part.TwitterTitle))
            {
                aspect.TwitterTitle = part.TwitterTitle;
            }
            else
            {
                aspect.TwitterTitle = part.PageTitle;
            }

            if (!string.IsNullOrEmpty(part.TwitterDescription))
            {
                aspect.TwitterDescription = part.TwitterDescription;
            }
            else
            {
                aspect.TwitterDescription = part.MetaDescription;
            }

            if (!string.IsNullOrEmpty(part.TwitterCard))
            {
                aspect.TwitterCard = part.TwitterCard;
            }

            if (!string.IsNullOrEmpty(part.TwitterCreator))
            {
                aspect.TwitterCreator = part.TwitterCreator;
            }

            if (!string.IsNullOrEmpty(part.TwitterSite))
            {
                aspect.TwitterSite = part.TwitterSite;
            }

            aspect.GoogleSchema = part.GoogleSchema;
        });
    }

    /// <summary>The absolute URL of a media field's first item the caller may view, or null.</summary>
    private Task<string> SocialImageUrlAsync(IUrlHelper urlHelper, MediaField field) =>
        SeoMediaUrls.UrlAsync(_references, urlHelper, field?.FirstReference());

    // Every save path: the canonical address holds no characters an address may not hold, and
    // the Google schema is JSON.
    protected override Task ValidatingAsync(ValidateContentPartContext context, SeoMetaPart part)
    {
        if (part.Canonical?.IndexOfAny(SeoMetaPart.InvalidCharactersForCanoncial) > -1 || part.Canonical?.IndexOf(' ') > -1)
        {
            context.Fail("The canonical entry contains invalid characters.", nameof(part.Canonical));
        }

        if (!string.IsNullOrWhiteSpace(part.GoogleSchema))
        {
            try
            {
                System.Text.Json.Nodes.JsonNode.Parse(part.GoogleSchema);
            }
            catch (System.Text.Json.JsonException)
            {
                context.Fail("The google schema is written in an incorrect format.", nameof(part.GoogleSchema));
            }
        }

        return Task.CompletedTask;
    }
}

internal static class SeoMediaUrls
{
    /// <summary>
    /// The absolute, unsigned URL of a reference the caller may view; a path that resolves to no
    /// placement (the library not yet imported) keeps its <c>/media</c> URL; otherwise null.
    /// </summary>
    public static async Task<string> UrlAsync(IMediaReferenceResolver references, IUrlHelper urlHelper, MediaReference? reference)
    {
        if (reference is not { IsEmpty: false } item)
        {
            return null;
        }

        var url = (await references.ResolveAsync(item, MediaUrlOptions.Unsigned))?.Url;
        if (url is null && !string.IsNullOrEmpty(item.Path))
        {
            url = await references.GetUrlAsync(item.Path, MediaUrlOptions.Unsigned);
        }

        return url is null ? null : Absolute(urlHelper, url);
    }

    /// <summary>A URL made absolute on the site's host, unless it already is (a CDN's).</summary>
    public static string Absolute(IUrlHelper urlHelper, string url) =>
        Uri.TryCreate(url, UriKind.Absolute, out _) && !url.StartsWith('/') ? url : urlHelper.ToAbsoluteUrl(url);

}
