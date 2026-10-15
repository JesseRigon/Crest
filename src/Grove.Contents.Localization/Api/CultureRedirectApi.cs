#nullable enable

using System.Globalization;
using Grove.Api;
using Grove.Contents.Localization.Models;
using Grove.Contents.Localization.Services;
using Grove.Data.Localization;
using Grove.Infrastructure.Settings;
using Grove.Routing;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace Grove.Contents.Localization.Api;

/// <summary>
/// <c>RedirectToLocalizedContent</c> (at the tenant's root, not under <c>api/</c>): the culture
/// switch a site page links to (the <c>CulturePicker</c> component, the <c>switch_culture_url</c>
/// Liquid filter). Sets the culture cookie when the settings say so and redirects to the current
/// item's translation in the target culture, else (as the settings say) the home page's, else
/// back to the page itself. The endpoint is named <see cref="RouteName"/> for link generation.
/// </summary>
public sealed class CultureRedirectApi : ApiContributor
{
    public const string RouteName = "RedirectToLocalizedContent";

    public override string Name => "ContentCultureRedirect";

    public override string FeatureId => "Grove.Contents.Localization.ContentCulturePicker";

    public override string RoutePrefix => string.Empty;

    public override void Map(IEndpointRouteBuilder group)
    {
        group.MapGet("RedirectToLocalizedContent", (
                HttpContext http,
                [FromQuery] string? targetCulture,
                [FromQuery] string? contentItemUrl,
                [FromQuery] string? queryStringValue) => ApiHandlers.For<CultureRedirectHandlers>(http).RedirectAsync(targetCulture, contentItemUrl, queryStringValue))
            .AllowAnonymous()
            .WithName(RouteName)
            .ExcludeFromDescription();
    }
}

public sealed class CultureRedirectHandlers(
    ISiteService siteService,
    ILocalizationService localizationService,
    IContentCulturePickerService culturePickerService,
    IShellLinkGenerator links) : ApiHandlers
{
    public async Task<IResult> RedirectAsync(string? targetCulture, string? contentItemUrl, string? queryStringValue)
    {
        targetCulture ??= CultureInfo.InvariantCulture.Name;

        var path = !string.IsNullOrEmpty(contentItemUrl) && contentItemUrl.StartsWith('/')
            ? new PathString(contentItemUrl)
            : new PathString("/");

        var queryString = string.IsNullOrEmpty(queryStringValue) ? QueryString.Empty : new QueryString(queryStringValue);
        var url = Request.PathBase + path + queryString;

        var supportedCultures = await localizationService.GetSupportedCulturesAsync();

        if (supportedCultures.Any(t => t == targetCulture))
        {
            var settings = await siteService.GetSettingsAsync<ContentCulturePickerSettings>();
            if (settings.SetCookie)
            {
                culturePickerService.SetContentCulturePickerCookie(targetCulture);
            }

            // Redirect the user to the Content with the same localizationSet as the ContentItem of the current url
            var localizations = await culturePickerService.GetLocalizationsFromRouteAsync(path);
            if (!TryGetLocalizedContentUrl(localizations) && settings.RedirectToHomepage)
            {
                // Try to get the Homepage for the current culture
                var homeLocalizations = await culturePickerService.GetLocalizationsFromRouteAsync("/");
                TryGetLocalizedContentUrl(homeLocalizations);
            }
        }

        bool TryGetLocalizedContentUrl(IEnumerable<LocalizationEntry> localizationEntries)
        {
            var localization = localizationEntries.SingleOrDefault(e => string.Equals(e.Culture, targetCulture, StringComparison.OrdinalIgnoreCase));
            if (localization is null)
            {
                return false;
            }

            var itemPath = links.GetPathByRouteValues(new RouteValueDictionary
            {
                ["area"] = "Grove.Contents",
                ["controller"] = "Item",
                ["action"] = "Display",
                ["contentItemId"] = localization.ContentItemId,
            });
            if (itemPath is null)
            {
                return false;
            }

            url = itemPath + queryString;
            return true;
        }

        return Results.LocalRedirect(url, permanent: true);
    }
}
