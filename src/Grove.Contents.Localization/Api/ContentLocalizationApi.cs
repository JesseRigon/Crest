#nullable enable

using System.Globalization;
using Grove.Api;
using Grove.Contents.Localization.Models;
using Grove.Contents.Management;
using Grove.Contents;
using Grove.Data.Localization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace Grove.Contents.Localization.Api;

/// <summary>
/// An item's translations (<c>api/grove/content-localization</c>): its culture and its
/// localization set's other cultures, each with the item that holds it, and the localize action
/// that creates a translation. The native editor's <c>LocalizationFieldSetEditor</c> reads and calls it.
/// </summary>
public sealed class ContentLocalizationApi : ApiContributor
{
    public override string Name => "ContentLocalization";

    public override string FeatureId => "Grove.Contents.Localization";

    public override void Map(IEndpointRouteBuilder group)
    {
        var routes = group.MapGroup("content-localization");
        routes.MapGet("{contentItemId}", (HttpContext __http, [FromRoute] string contentItemId) => ApiHandlers.For<ContentLocalizationHandlers>(__http).GetAsync(contentItemId));
        routes.MapPost("{contentItemId}/localize", (HttpContext __http, [FromRoute] string contentItemId, [FromBody] LocalizeRequest request) => ApiHandlers.For<ContentLocalizationHandlers>(__http).LocalizeAsync(contentItemId, request));
    }
}

/// <summary>The culture to create a translation in; the invariant culture is the empty name.</summary>
public sealed record LocalizeRequest(string? Culture);

/// <summary>An item's culture and every culture of its set: the site's cultures, and any a translation still holds after its culture was removed.</summary>
public sealed record ContentLocalizationInfo(string Culture, string CultureDisplayName, string? LocalizationSet, IReadOnlyList<ContentTranslation> Translations);

/// <summary>One culture of the set: the item holding it (none yet), and whether the culture is no longer a site culture.</summary>
public sealed record ContentTranslation(string Culture, string DisplayName, string? ContentItemId, bool IsDeleted);

/// <summary>The created translation.</summary>
public sealed record LocalizeResult(string ContentItemId);

public sealed class ContentLocalizationHandlers(
    IContentManager contentManager,
    IContentLocalizationManager localizationManager,
    ILocalizationService localizationService,
    IAuthorizationService authorizationService) : ApiHandlers
{
    public async Task<IResult> GetAsync(string contentItemId)
    {
        var item = await contentManager.GetAsync(contentItemId, VersionOptions.Latest);
        if (item is null || !item.TryGet<LocalizationFieldSet>(out var part))
        {
            return NotFound();
        }

        if (!await authorizationService.AuthorizeAsync(User, CommonPermissions.EditContent, item))
        {
            return Forbid();
        }

        var culture = part.Culture ?? await localizationService.GetDefaultCultureAsync();
        var translated = string.IsNullOrEmpty(part.LocalizationSet)
            ? []
            : (await localizationManager.GetItemsForSetAsync(part.LocalizationSet)).ToList();
        var supported = await localizationService.GetSupportedCulturesAsync();

        var translations = supported
            .Where(name => !string.Equals(name, culture, StringComparison.OrdinalIgnoreCase))
            .Select(name => new ContentTranslation(name, DisplayName(name), translated.FirstOrDefault(other => CultureOf(other) == name)?.ContentItemId, IsDeleted: false))
            .ToList();

        // Translations whose culture was removed from the site's cultures stay reachable.
        translations.AddRange(translated
            .Where(other => other.ContentItemId != item.ContentItemId
                && CultureOf(other) is { } name
                && !string.Equals(name, culture, StringComparison.OrdinalIgnoreCase)
                && translations.All(known => known.Culture != name))
            .Select(other => new ContentTranslation(CultureOf(other)!, DisplayName(CultureOf(other)!), other.ContentItemId, IsDeleted: true)));

        return Ok(new ContentLocalizationInfo(culture, DisplayName(culture), part.LocalizationSet, translations));
    }

    public async Task<IResult> LocalizeAsync(string contentItemId, [FromBody] LocalizeRequest request)
    {
        // The invariant culture's name is empty.
        var targetCulture = request.Culture ?? string.Empty;
        var item = await contentManager.GetAsync(contentItemId, VersionOptions.Latest);
        if (item is null || !item.TryGet<LocalizationFieldSet>(out var part))
        {
            return NotFound();
        }

        if (!await authorizationService.AuthorizeAsync(User, ContentLocalizationPermissions.LocalizeContent, item)
            || !await authorizationService.AuthorizeAsync(User, CommonPermissions.EditContent, item))
        {
            return Forbid();
        }

        if (string.IsNullOrEmpty(part.LocalizationSet))
        {
            return ApiProblems.BadRequest("Save the item before creating its translations.");
        }

        if (await localizationManager.GetContentItemAsync(part.LocalizationSet, targetCulture) is { } existing)
        {
            return ApiProblems.Conflict($"A translation in '{targetCulture}' already exists ({existing.ContentItemId}).");
        }

        try
        {
            var created = await localizationManager.LocalizeAsync(item, targetCulture);
            return Ok(new LocalizeResult(created.ContentItemId));
        }
        catch (InvalidOperationException exception)
        {
            return ApiProblems.Conflict(exception.Message);
        }
    }

    private static string? CultureOf(ContentItem item) => item.TryGet<LocalizationFieldSet>(out var part) ? part.Culture : null;

    private static string DisplayName(string culture)
    {
        try
        {
            var info = CultureInfo.GetCultureInfo(culture);
            return string.IsNullOrEmpty(info.Name) ? "Invariant" : $"{info.DisplayName} ({info.Name})";
        }
        catch (CultureNotFoundException)
        {
            return culture;
        }
    }
}
