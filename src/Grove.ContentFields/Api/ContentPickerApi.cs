#nullable enable

using Grove.Api;
using Grove.ContentFields.Settings;
using Grove.ContentLocalization;
using Grove.ContentManagement;
using Grove.ContentManagement.Metadata;
using Grove.ContentManagement.Metadata.Models;
using Grove.ContentManagement.Records;
using Grove.Contents;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using YesSql;
using YesSql.Services;

namespace Grove.ContentFields.Api;

/// <summary>
/// The content picker's candidates (<c>api/grove/content-fields/content-items</c>): the items a
/// content picker field may hold, searched by name through the field's result provider (its
/// editor's, else the default), or the named ones (<c>ids</c>) to show what a field holds; and,
/// with Content Localization, the localization sets a localization set picker may hold
/// (<c>.../localization-sets</c>). The native <c>ContentPickerFieldEditor</c> calls it.
/// </summary>
public sealed class ContentPickerApi : ApiContributor
{
    public override string Name => "ContentPicker";

    public override string FeatureId => "Grove.ContentFields";

    public override void Map(IEndpointRouteBuilder group)
    {
        var routes = group.MapGroup("content-fields");
        routes.MapGet("content-items", (HttpContext __http, [FromQuery] string? part, [FromQuery] string? field, [FromQuery] string? query = null, [FromQuery] string? ids = null)
            => ApiHandlers.For<ContentPickerHandlers>(__http).SearchAsync(part, field, query, ids));
        routes.MapGet("localization-sets", (HttpContext __http, [FromQuery] string? part, [FromQuery] string? field, [FromQuery] string? query = null, [FromQuery] string? ids = null)
            => ApiHandlers.For<ContentPickerHandlers>(__http).LocalizationSetsAsync(part, field, query, ids));
    }
}

/// <summary>A candidate: its id (a localization set's for that picker), its name, whether it has a published version, and whether the caller may edit it.</summary>
public sealed record ContentPickerItem(string Id, string? DisplayText, string? ContentType, bool HasPublished, bool IsEditable);

/// <summary>The candidates, and the types the caller may create one of from the picker.</summary>
public sealed record ContentPickerResults(IReadOnlyList<ContentPickerItem> Items, IReadOnlyList<ContentPickerType> CreatableTypes);

public sealed record ContentPickerType(string Name, string DisplayName);

public sealed class ContentPickerHandlers(
    IContentDefinitionManager definitions,
    IContentManager contentManager,
    IEnumerable<IContentPickerResultProvider> resultProviders,
    IAuthorizationService authorizationService,
    YesSql.ISession session) : ApiHandlers
{
    public async Task<IResult> SearchAsync(string? part, string? field, string? query, string? ids)
    {
        if (await FieldAsync(part, field) is not { } definition || definition.GetSettings<ContentPickerFieldSettings>() is not { } settings)
        {
            return ApiProblems.BadRequest("The part and the field of a content picker are required.");
        }

        var contentTypes = settings.DisplayedContentTypes ?? [];
        if (settings.DisplayedStereotypes is { Length: > 0 } stereotypes)
        {
            contentTypes = [.. (await definitions.ListTypeDefinitionsAsync())
                .Where(type => type.TryGetStereotype(out var stereotype) && stereotypes.Contains(stereotype))
                .Select(type => type.Name)];
        }

        var items = new List<ContentPickerItem>();
        if (!string.IsNullOrWhiteSpace(ids))
        {
            // What the field holds, for its chips: only what the caller may see.
            foreach (var item in await contentManager.GetAsync(ids.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries), VersionOptions.Latest))
            {
                if (await authorizationService.AuthorizeAsync(User, CommonPermissions.ViewContent, item))
                {
                    items.Add(new ContentPickerItem(item.ContentItemId, item.DisplayText, item.ContentType, await contentManager.HasPublishedVersionAsync(item), await authorizationService.AuthorizeAsync(User, CommonPermissions.EditContent, item)));
                }
            }
        }
        else
        {
            var editor = definition.Editor() ?? "Default";
            var provider = resultProviders.FirstOrDefault(candidate => candidate.Name == editor) ?? resultProviders.FirstOrDefault(candidate => candidate.Name == "Default");
            var results = provider is null ? [] : await provider.Search(new ContentPickerSearchContext
            {
                Query = query,
                DisplayAllContentTypes = settings.DisplayAllContentTypes,
                ContentTypes = contentTypes,
                PartFieldDefinition = definition,
            });

            var missing = results.Where(result => result.ContentItem is null).Select(result => result.ContentItemId).Distinct();
            var fetched = (await contentManager.GetAsync(missing)).ToDictionary(item => item.ContentItemId);
            foreach (var result in results)
            {
                var item = result.ContentItem ?? fetched.GetValueOrDefault(result.ContentItemId);
                var editable = item is not null && await authorizationService.AuthorizeAsync(User, CommonPermissions.EditContent, item);
                items.Add(new ContentPickerItem(result.ContentItemId, result.DisplayText, item?.ContentType, result.HasPublished, editable));
            }
        }

        // The picker's create links: the creatable types it may hold that the caller may create.
        var creatable = new List<ContentPickerType>();
        var candidates = settings.DisplayAllContentTypes
            ? await definitions.ListTypeDefinitionsAsync()
            : (await definitions.ListTypeDefinitionsAsync()).Where(type => contentTypes.Contains(type.Name));
        foreach (var type in candidates.Where(type => type.IsCreatable()))
        {
            var probe = await contentManager.NewAsync(type.Name);
            if (await authorizationService.AuthorizeAsync(User, CommonPermissions.EditContent, probe))
            {
                creatable.Add(new ContentPickerType(type.Name, type.DisplayName));
            }
        }

        return Ok(new ContentPickerResults(items, creatable));
    }

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
