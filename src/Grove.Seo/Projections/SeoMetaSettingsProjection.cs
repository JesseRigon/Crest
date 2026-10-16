using Grove.Routing;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Grove.Contents.Management;
using Grove.Contents.Management.Handlers;
using Grove.Display.Management.Extensions;
using Grove.Entities;
using System.Text.Json.Nodes;
using Grove.Media;
using Grove.Media.Fields;
using Grove.Media.Library;
using Grove.Seo.Models;
using Grove.Infrastructure.Settings;

namespace Grove.Seo.Projections;

/// <summary>The site's default search and social metadata for any item (a read projection).</summary>
public sealed class SeoMetaSettingsProjection : IContentProjection
{
    private readonly IMediaReferenceResolver _references;
    private readonly ISiteService _siteService;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IShellLinkGenerator _links;
    private IContentManager _contentManager;

    public SeoMetaSettingsProjection(
        IMediaReferenceResolver references,
        ISiteService siteService,
        IHttpContextAccessor httpContextAccessor,
        IShellLinkGenerator links
        )
    {
        _references = references;
        _siteService = siteService;
        _httpContextAccessor = httpContextAccessor;
        _links = links;
    }

    public Task ProjectAsync(ContentItemAspectContext context)
    {
        return context.ForAsync<SeoAspect>(async aspect =>
        {
            // This handlers provides defaults, either from the Seo Meta Settings, or ensures values by default. (title etc)
            _contentManager ??= _httpContextAccessor.HttpContext.RequestServices.GetRequiredService<IContentManager>();
            var siteSettings = await _siteService.GetSiteSettingsAsync();

            var urlHelper = _links;
            var contentItemMetadata = await _contentManager.PopulateAspectAsync<ContentItemMetadata>(context.ContentItem);

            var relativeUrl = urlHelper.GetPathByRouteValues(contentItemMetadata.DisplayRouteValues);
            var absoluteUrl = urlHelper.ToAbsoluteUrl(relativeUrl);

            // Logic is this happens last after the part settings.
            // so if values are not set it is responsible for settings them.

            MediaReference? defaultImage = null;
            MediaReference? openGraphImage = null;
            MediaReference? twitterImage = null;

            var defaultAltText = string.Empty;
            var openGraphAltText = string.Empty;
            var twitterAltText = string.Empty;

            var defaultMetaDescription = string.Empty;
            var openGraphType = string.Empty;
            var defaultOpenGraphDescription = string.Empty;
            var openGraphSiteName = string.Empty;
            var openGraphAppId = string.Empty;
            var defaultTwitterDescription = string.Empty;
            var twitterCard = string.Empty;
            var twitterCreator = string.Empty;
            var twitterSite = string.Empty;
            var googleSchema = string.Empty;

            if (siteSettings.TryGet<ContentItem>("SocialMetaSettings", out var metaSettings))
            {
                dynamic socialMetaSettings = metaSettings.Content?.SocialMetaSettings;

                if (socialMetaSettings is not null)
                {
                    defaultImage = FirstMedia(socialMetaSettings.DefaultSocialImage);

                    openGraphImage = FirstMedia(socialMetaSettings.OpenGraphImage);

                    twitterImage = FirstMedia(socialMetaSettings.TwitterImage);

                    if (socialMetaSettings.DefaultSocialImage?.MediaTexts?.Count > 0)
                    {
                        defaultAltText = socialMetaSettings.DefaultSocialImage.MediaTexts[0];
                    }

                    if (socialMetaSettings.OpenGraphImage?.MediaTexts?.Count > 0)
                    {
                        openGraphAltText = socialMetaSettings.OpenGraphImage.MediaTexts[0];
                    }

                    if (socialMetaSettings.TwitterImage?.MediaTexts?.Count > 0)
                    {
                        twitterAltText = socialMetaSettings.TwitterImage.MediaTexts[0];
                    }

                    if (socialMetaSettings.DefaultMetaDescription?.Text is not null)
                    {
                        defaultMetaDescription = socialMetaSettings.DefaultMetaDescription.Text.ToString();
                    }

                    if (socialMetaSettings.OpenGraphType?.Text is not null)
                    {
                        openGraphType = socialMetaSettings.OpenGraphType.Text.ToString();
                    }

                    if (socialMetaSettings.DefaultOpenGraphDescription?.Text is not null)
                    {
                        defaultOpenGraphDescription = socialMetaSettings.DefaultOpenGraphDescription.Text.ToString();
                    }

                    if (socialMetaSettings.OpenGraphSiteName?.Text is not null)
                    {
                        openGraphSiteName = socialMetaSettings.OpenGraphSiteName.Text.ToString();
                    }

                    if (socialMetaSettings.OpenGraphAppId?.Text is not null)
                    {
                        openGraphAppId = socialMetaSettings.OpenGraphAppId.Text.ToString();
                    }

                    if (socialMetaSettings.DefaultTwitterDescription?.Text is not null)
                    {
                        defaultTwitterDescription = socialMetaSettings.DefaultTwitterDescription.Text.ToString();
                    }

                    if (socialMetaSettings.TwitterCard?.Text is not null)
                    {
                        twitterCard = socialMetaSettings.TwitterCard.Text.ToString();
                    }

                    if (socialMetaSettings.TwitterCreator?.Text is not null)
                    {
                        twitterCreator = socialMetaSettings.TwitterCreator.Text.ToString();
                    }

                    if (socialMetaSettings.TwitterSite?.Text is not null)
                    {
                        twitterSite = socialMetaSettings.TwitterSite.Text.ToString();
                    }

                    if (socialMetaSettings.GoogleSchema?.Text is not null)
                    {
                        googleSchema = socialMetaSettings.GoogleSchema.Text.ToString();
                    }
                }
            }

            // Meta
            if (string.IsNullOrEmpty(aspect.MetaDescription))
            {
                aspect.MetaDescription = defaultMetaDescription;
            }

            // OpenGraph
            aspect.OpenGraphUrl = aspect.Canonical ??= absoluteUrl;

            if (string.IsNullOrEmpty(aspect.OpenGraphType))
            {
                aspect.OpenGraphType = openGraphType;
            }

            if (string.IsNullOrEmpty(aspect.OpenGraphTitle))
            {
                aspect.OpenGraphTitle = context.ContentItem.DisplayText;
            }

            if (string.IsNullOrEmpty(aspect.OpenGraphDescription))
            {
                aspect.OpenGraphDescription = defaultOpenGraphDescription;
            }

            if (string.IsNullOrEmpty(aspect.OpenGraphSiteName))
            {
                aspect.OpenGraphSiteName = openGraphSiteName;
                if (string.IsNullOrEmpty(aspect.OpenGraphSiteName))
                {
                    aspect.OpenGraphSiteName = siteSettings.SiteName;
                }
            }

            if (string.IsNullOrEmpty(aspect.OpenGraphAppId))
            {
                aspect.OpenGraphAppId = openGraphAppId;
            }

            if (string.IsNullOrEmpty(aspect.OpenGraphImage))
            {
                if (openGraphImage is null)
                {
                    if (defaultImage is not null)
                    {
                        aspect.OpenGraphImage = await SocialImageUrlAsync(urlHelper, defaultImage);
                    }
                }
                else
                {
                    aspect.OpenGraphImage = await SocialImageUrlAsync(urlHelper, openGraphImage);
                }
            }

            if (string.IsNullOrEmpty(aspect.OpenGraphImageAlt))
            {
                if (string.IsNullOrEmpty(openGraphAltText))
                {
                    aspect.OpenGraphImageAlt = defaultAltText;
                }
                else
                {
                    aspect.OpenGraphImageAlt = openGraphAltText;
                }
            }

            // Twitter
            aspect.TwitterUrl = aspect.Canonical ??= absoluteUrl;

            if (string.IsNullOrEmpty(aspect.TwitterTitle))
            {
                aspect.TwitterTitle = context.ContentItem.DisplayText;
            }

            if (string.IsNullOrEmpty(aspect.TwitterDescription))
            {
                aspect.TwitterDescription = defaultTwitterDescription;
            }

            if (string.IsNullOrEmpty(aspect.TwitterCard))
            {
                aspect.TwitterCard = twitterCard;
            }

            if (string.IsNullOrEmpty(aspect.TwitterSite))
            {
                aspect.TwitterSite = twitterSite;
            }

            if (string.IsNullOrEmpty(aspect.TwitterCreator))
            {
                aspect.TwitterCreator = twitterCreator;
            }

            if (string.IsNullOrEmpty(aspect.TwitterImage))
            {
                if (twitterImage is null)
                {
                    if (defaultImage is not null)
                    {
                        aspect.TwitterImage = await SocialImageUrlAsync(urlHelper, defaultImage);
                    }
                }
                else
                {
                    aspect.TwitterImage = await SocialImageUrlAsync(urlHelper, twitterImage);
                }
            }

            if (string.IsNullOrEmpty(aspect.TwitterImageAlt))
            {
                if (string.IsNullOrEmpty(twitterAltText))
                {
                    aspect.TwitterImageAlt = defaultAltText;
                }
                else
                {
                    aspect.TwitterImageAlt = twitterAltText;
                }
            }

            if (string.IsNullOrEmpty(aspect.GoogleSchema))
            {
                aspect.GoogleSchema = googleSchema;
            }
        });
    }

    /// <summary>The first item of a media field in the settings JSON (ruling F1).</summary>
    private static MediaReference? FirstMedia(object field)
    {
        var json = field switch
        {
            JsonObject obj => obj,
            System.Text.Json.Dynamic.JsonDynamicObject dynamicObject => (JsonObject)dynamicObject,
            _ => null,
        };

        return MediaFieldReferences.FromJson(json)?.FirstReference();
    }

    private Task<string> SocialImageUrlAsync(IShellLinkGenerator urlHelper, MediaReference? reference) =>
        SeoMediaUrls.UrlAsync(_references, urlHelper, reference);
}
