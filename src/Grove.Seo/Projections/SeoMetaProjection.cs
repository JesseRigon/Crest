using Grove.Routing;
using Microsoft.AspNetCore.Http;
using Grove.Contents.Management;
using Grove.Contents.Management.Handlers;
using Grove.Display.Management.Extensions;
using Grove.Media;
using Grove.Media.Fields;
using Grove.Media.Library;
using Grove.Seo.Models;
using Grove.Infrastructure.Settings;

namespace Grove.Seo.Projections;

/// <summary>An item's search and social metadata (a read projection) from its SEO field set.</summary>
public sealed class SeoMetaProjection : ContentFieldSetProjection<SeoMetaFieldSet>
{
    private readonly IMediaReferenceResolver _references;
    private readonly ISiteService _siteService;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IShellLinkGenerator _links;
    private readonly IContentManager _contentManager;

    public SeoMetaProjection(
        IMediaReferenceResolver references,
        ISiteService siteService,
        IHttpContextAccessor httpContextAccessor,
        IShellLinkGenerator links,
        IContentManager contentManager
        )
    {
        _references = references;
        _siteService = siteService;
        _httpContextAccessor = httpContextAccessor;
        _links = links;
        _contentManager = contentManager;
    }

    protected override Task ProjectAsync(ContentItemAspectContext context, SeoMetaFieldSet part)
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

            var urlHelper = _links;

            if (!string.IsNullOrEmpty(part.Canonical))
            {
                aspect.Canonical = part.Canonical;
            }
            else
            {
                var contentItemMetadata = await _contentManager.PopulateAspectAsync<ContentItemMetadata>(part.ContentItem);
                var relativeUrl = urlHelper.GetPathByRouteValues(contentItemMetadata.DisplayRouteValues);
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
    private Task<string> SocialImageUrlAsync(IShellLinkGenerator urlHelper, MediaField field) =>
        SeoMediaUrls.UrlAsync(_references, urlHelper, field?.FirstReference());
}

internal static class SeoMediaUrls
{
    /// <summary>
    /// The absolute, unsigned URL of a reference the caller may view; a path that resolves to no
    /// placement (the library not yet imported) keeps its <c>/media</c> URL; otherwise null.
    /// </summary>
    public static async Task<string> UrlAsync(IMediaReferenceResolver references, IShellLinkGenerator urlHelper, MediaReference? reference)
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
    public static string Absolute(IShellLinkGenerator urlHelper, string url) =>
        Uri.TryCreate(url, UriKind.Absolute, out _) && !url.StartsWith('/') ? url : urlHelper.ToAbsoluteUrl(url);

}
