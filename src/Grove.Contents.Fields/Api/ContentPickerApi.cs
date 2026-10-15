#nullable enable

using Grove.Api;
using Grove.Contents.Fields.Settings;
using Grove.Contents.Localization;
using Grove.Contents.Management;
using Grove.Contents.Management.Metadata;
using Grove.Contents.Management.Metadata.Models;
using Grove.Contents.Management.Records;
using Grove.Contents;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using YesSql;
using YesSql.Services;

namespace Grove.Contents.Fields.Api;

/// <summary>
/// The localization set picker's candidates (<c>api/grove/content-fields/localization-sets</c>,
/// with Content Localization): the sets a localization set picker field may hold, searched by
/// name, or the named ones (<c>ids</c>) to show what a field holds. The native
/// <c>LocalizationSetPickerFieldEditor</c> calls it. (Content items are picked through the
/// option picker's content items source.)
/// </summary>
public sealed class ContentPickerApi : ApiContributor
{
    public override string Name => "ContentPicker";

    public override string FeatureId => "Grove.Contents.Fields";

    public override void Map(IEndpointRouteBuilder group)
    {
        var routes = group.MapGroup("content-fields");
        routes.MapGet("localization-sets", (HttpContext __http, [FromQuery] string? part, [FromQuery] string? field, [FromQuery] string? query = null, [FromQuery] string? ids = null)
            => ApiHandlers.For<ContentPickerHandlers>(__http).LocalizationSetsAsync(part, field, query, ids));
    }
}

/// <summary>A candidate: its id (a localization set's for that picker), its name, whether it has a published version, and whether the caller may edit it.</summary>
public sealed record ContentPickerItem(string Id, string? DisplayText, string? ContentType, bool HasPublished, bool IsEditable);

/// <summary>The candidates.</summary>
public sealed record ContentPickerResults(IReadOnlyList<ContentPickerItem> Items, IReadOnlyList<ContentPickerType> CreatableTypes);

public sealed record ContentPickerType(string Name, string DisplayName);

public sealed class ContentPickerHandlers(
    IContentDefinitionManager definitions,
    IContentManager contentManager,
    IAuthorizationService authorizationService,
    YesSql.ISession session) : ApiHandlers
{
    public async Task<IResult> LocalizationSetsAsync(string? part, string? field, string? query, string? ids)
    {
        if (HttpContext.RequestServices.GetService<IContentLocalizationManager>() is not { } localizations)
        {
            return NotFound();
        }

        if (await FieldAsync(part, field) is not { } definition || definition.GetSettings<LocalizationSetContentPickerFieldSettings>() is not { } settings)
        {
            return ApiProblems.BadRequest("The part and the field of a localization set picker are required.");
        }

        IEnumerable<KeyValuePair<string, ContentItem>> sets;
        if (!string.IsNullOrWhiteSpace(ids))
        {
            var named = ids.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            var first = await localizations.GetFirstItemIdForSetsAsync(named);
            sets = (await contentManager.GetAsync(first.Values, VersionOptions.Latest))
                .Select(item => KeyValuePair.Create(first.First(pair => pair.Value == item.ContentItemId).Key, item));
        }
        else
        {
            var types = settings.DisplayedContentTypes ?? [];
            var search = session.Query<ContentItem, ContentItemIndex>().With<ContentItemIndex>(index => index.ContentType.IsIn(types) && index.Latest);
            if (!string.IsNullOrEmpty(query))
            {
                search.With<ContentItemIndex>(index => index.DisplayText.Contains(query) || index.ContentType.Contains(query));
            }

            // One item per set, in the current culture when there is one.
            sets = await localizations.DeduplicateContentItemsAsync(await search.Take(40).ListAsync());
        }

        var items = new List<ContentPickerItem>();
        foreach (var (set, item) in sets)
        {
            if (await authorizationService.AuthorizeAsync(User, CommonPermissions.ViewContent, item))
            {
                items.Add(new ContentPickerItem(set, item.DisplayText, item.ContentType, await contentManager.HasPublishedVersionAsync(item), await authorizationService.AuthorizeAsync(User, CommonPermissions.EditContent, item)));
            }
        }

        return Ok(new ContentPickerResults(items, []));
    }

    private async Task<ContentPartFieldDefinition?> FieldAsync(string? part, string? field) =>
        string.IsNullOrWhiteSpace(part) || string.IsNullOrWhiteSpace(field)
            ? null
            : (await definitions.GetPartDefinitionAsync(part))?.Fields.FirstOrDefault(candidate => candidate.Name == field);
}
