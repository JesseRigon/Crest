#nullable enable

using Grove.Api;
using Grove.Contents.Management;
using Grove.Contents.Management.Metadata;
using Grove.Contents.Management.Metadata.Models;
using Grove.Contents;
using Grove.Contents.Hierarchy.Models;
using Grove.Contents.Hierarchy.Services;
using Grove.Display.Navigation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace Grove.Contents.Hierarchy.Api;

/// <summary>
/// A list's contained items (<c>api/grove/lists/{containerId}</c>): a page of them (searched,
/// filtered by status, in the list's order when it is ordered), the types a child may be
/// created as, and the reorder action. The native editor's <c>ChildContentItemsFieldSetEditor</c> reads and calls
/// it; a child is created on the native create page with the list as its container.
/// </summary>
public sealed class HierarchyApi : ApiContributor
{
    public override string Name => "Hierarchy";

    public override string FeatureId => "Grove.Contents.Hierarchy";

    public override void Map(IEndpointRouteBuilder group)
    {
        var routes = group.MapGroup("lists/{containerId}");
        routes.MapGet("items", (HttpContext __http, [FromRoute] string containerId, [FromQuery] string? search = null, [FromQuery] string? status = null, [FromQuery] int page = 1, [FromQuery] int pageSize = 0)
            => ApiHandlers.For<HierarchyHandlers>(__http).ItemsAsync(containerId, search, status, page, pageSize));
        routes.MapPost("order", (HttpContext __http, [FromRoute] string containerId, [FromBody] ListOrderRequest request)
            => ApiHandlers.For<HierarchyHandlers>(__http).OrderAsync(containerId, request));
    }
}

/// <summary>A page of a list's items, in their new order.</summary>
public sealed record ListOrderRequest(string[] ContentItemIds);

/// <summary>A contained item as the list editor shows it.</summary>
public sealed record ListItem(string ContentItemId, string ContentType, string? DisplayText, bool Published, bool Latest, DateTime? ModifiedUtc, int Order);

/// <summary>A type a child of the list may be created as.</summary>
public sealed record ListContainedType(string Name, string DisplayName);

/// <summary>A page of the list, with what the editor needs to offer around it.</summary>
public sealed record ListPage(IReadOnlyList<ListItem> Items, int Total, int Page, int PageSize, bool EnableOrdering, IReadOnlyList<ListContainedType> ContainedTypes);

public sealed class HierarchyHandlers(
    IContentManager contentManager,
    IContentDefinitionManager definitions,
    IContainerService containers,
    IAuthorizationService authorizationService) : ApiHandlers
{
    public async Task<IResult> ItemsAsync(string containerId, string? search, string? status, int page, int pageSize)
    {
        var container = await contentManager.GetAsync(containerId, VersionOptions.Latest);
        if (container is null || await SettingsAsync(container) is not { } settings)
        {
            return NotFound();
        }

        if (!await authorizationService.AuthorizeAsync(User, CommonPermissions.EditContent, container))
        {
            return Forbid();
        }

        var options = new ContainedItemOptions
        {
            DisplayText = string.IsNullOrWhiteSpace(search) ? null : search.Trim(),
            Status = status?.ToLowerInvariant() switch
            {
                "published" => ContentsStatus.Published,
                "draft" => ContentsStatus.Draft,
                _ => ContentsStatus.Latest,
            },
        };

        var pager = new Pager(Math.Max(1, page), Math.Clamp(pageSize <= 0 ? settings.PageSize : pageSize, 1, 100), settings.PageSize);
        var total = await containers.GetItemCountAsync(containerId, options);
        var items = await containers.QueryContainedItemsAsync(containerId, settings.EnableOrdering, pager, options);

        var types = new List<ListContainedType>();
        foreach (var name in settings.ContainedContentTypes ?? [])
        {
            if (await definitions.GetTypeDefinitionAsync(name) is { } definition)
            {
                types.Add(new ListContainedType(definition.Name, definition.DisplayName));
            }
        }

        return Ok(new ListPage(
            [.. items.Select(item => new ListItem(item.ContentItemId, item.ContentType, item.DisplayText, item.Published, item.Latest, item.ModifiedUtc, item.TryGet<ParentContentItemFieldSet>(out var contained) ? contained.Order : 0))],
            total,
            pager.Page,
            pager.PageSize,
            settings.EnableOrdering,
            types));
    }

    public async Task<IResult> OrderAsync(string containerId, [FromBody] ListOrderRequest request)
    {
        var container = await contentManager.GetAsync(containerId, VersionOptions.Latest);
        if (container is null || await SettingsAsync(container) is not { EnableOrdering: true })
        {
            return NotFound();
        }

        if (request.ContentItemIds is not { Length: > 0 } ids)
        {
            return ApiProblems.Validation(nameof(ListOrderRequest.ContentItemIds), "The items to order are required.");
        }

        var items = new List<ContentItem>();
        foreach (var id in ids)
        {
            var item = await contentManager.GetAsync(id, VersionOptions.Latest);
            if (item is null || !item.TryGet<ParentContentItemFieldSet>(out var contained) || contained.ListContentItemId != containerId)
            {
                return ApiProblems.BadRequest($"'{id}' is not an item of this list.");
            }

            // Reordering changes what is published, as the retired order action required.
            if (!await authorizationService.AuthorizeAsync(User, CommonPermissions.PublishContent, item))
            {
                return Forbid();
            }

            items.Add(item);
        }

        // The page keeps its place in the list: its first position is the lowest order it held.
        var first = items.Min(item => item.As<ParentContentItemFieldSet>().Order);
        await containers.UpdateContentItemOrdersAsync(items, first);
        return NoContent();
    }

    private async Task<ChildContentItemsFieldSetSettings?> SettingsAsync(ContentItem container)
    {
        var definition = await definitions.GetTypeDefinitionAsync(container.ContentType);
        return definition?.FieldSets.FirstOrDefault(part => part.FieldSetDefinition.Name == ContentFieldSetName.Of<ChildContentItemsFieldSet>())?.GetSettings<ChildContentItemsFieldSetSettings>();
    }
}
