#nullable enable

using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Grove.Api;
using Grove;
using Grove.Contents.Management;
using Grove.Contents.Management.Metadata;
using Grove.Contents.Management.Metadata.Models;
using Grove.Contents;
using Grove.Contents.SiteMenu;
using Grove.Contents.SiteMenu.Models;
using PlatformContentItem = Grove.Contents.Management.ContentItem;

namespace Grove.Contents.SiteMenu.Api;

public sealed class StandardMenusApi : ApiContributor
{
    public override string Name => "StandardMenus";

    public override string FeatureId => "Grove.Contents.SiteMenu";

    public override void Map(IEndpointRouteBuilder group)
    {
        var routes = group.MapGroup("menus");
        routes.MapGet("", ApiHandlers.Handle<StandardMenusHandlers>(h => h.ListAsync()));
        routes.MapPost("", (HttpContext __http, [FromBody] StandardMenuEditModel model) => ApiHandlers.For<StandardMenusHandlers>(__http).CreateMenuAsync(model));
        routes.MapPost("{menuId}/rename", (HttpContext __http, [FromRoute] string menuId, [FromBody] StandardMenuEditModel model) => ApiHandlers.For<StandardMenusHandlers>(__http).RenameMenuAsync(menuId, model));
        routes.MapPost("{menuId}/toggle", (HttpContext __http, [FromRoute] string menuId) => ApiHandlers.For<StandardMenusHandlers>(__http).ToggleMenuAsync(menuId));
        routes.MapPost("{menuId}/duplicate", (HttpContext __http, [FromRoute] string menuId) => ApiHandlers.For<StandardMenusHandlers>(__http).DuplicateMenuAsync(menuId));
        routes.MapDelete("{menuId}", (HttpContext __http, [FromRoute] string menuId) => ApiHandlers.For<StandardMenusHandlers>(__http).DeleteMenuAsync(menuId));
        routes.MapPost("{menuId}/nodes", (HttpContext __http, [FromRoute] string menuId, [FromBody] StandardMenuNodeEditModel model) => ApiHandlers.For<StandardMenusHandlers>(__http).CreateNodeAsync(menuId, model));
        routes.MapPut("{menuId}/nodes/{nodeId}", (HttpContext __http, [FromRoute] string menuId, [FromRoute] string nodeId, [FromBody] StandardMenuNodeEditModel model) => ApiHandlers.For<StandardMenusHandlers>(__http).UpdateNodeAsync(menuId, nodeId, model));
        routes.MapPost("{menuId}/nodes/{nodeId}/move", (HttpContext __http, [FromRoute] string menuId, [FromRoute] string nodeId, [FromBody] StandardMenuNodeMoveModel model) => ApiHandlers.For<StandardMenusHandlers>(__http).MoveNodeAsync(menuId, nodeId, model));
        routes.MapPost("{menuId}/nodes/{nodeId}/duplicate", (HttpContext __http, [FromRoute] string menuId, [FromRoute] string nodeId) => ApiHandlers.For<StandardMenusHandlers>(__http).DuplicateNodeAsync(menuId, nodeId));
        routes.MapDelete("{menuId}/nodes/{nodeId}", (HttpContext __http, [FromRoute] string menuId, [FromRoute] string nodeId) => ApiHandlers.For<StandardMenusHandlers>(__http).DeleteNodeAsync(menuId, nodeId));
    }
}

public sealed class StandardMenusHandlers(
    IAuthorizationService authorizationService,
    IPlatformHelper platformHelper,
    IContentManager contentManager,
    IContentDefinitionManager contentDefinitionManager) : ApiHandlers
{
    public async Task<IResult> ListAsync()
    {
        if (!await authorizationService.AuthorizeAsync(User, Grove.Contents.SiteMenu.Permissions.ManageMenu))
        {
            return Forbid();
        }

        var menus = await platformHelper.QueryContentItemsAsync(query => query
            .Where(index => index.ContentType == "Menu" && index.Latest)
            .OrderBy(index => index.DisplayText));

        var nodeTypes = await GetAvailableNodeTypesAsync();
        return Ok(new StandardMenusState(menus.Select(menu => StandardMenuSummary.From(menu)).ToArray(), nodeTypes));
    }

    public async Task<IResult> CreateMenuAsync(StandardMenuEditModel model)
    {
        if (!await IsAuthorizedAsync())
        {
            return Forbid();
        }

        var name = model.Name!.Trim();

        var menu = await contentManager.NewAsync("Menu");
        menu.DisplayText = name;
        menu.Alter<ContentFieldSet>("Title", title => title.Alter<Grove.Contents.Fields.Fields.TextField>("Title", text => text.Text = name));
        EnsureMenuItems(menu.Content);

        // A new menu is created (its id, version and lifecycle), published at once.
        await contentManager.CreateAsync(menu, VersionOptions.Published);

        return Ok(StandardMenuSummary.From(menu));
    }

    public async Task<IResult> RenameMenuAsync(string menuId, StandardMenuEditModel model)
    {
        if (!await IsAuthorizedAsync())
        {
            return Forbid();
        }

        var name = model.Name!.Trim();

        var menu = await LoadMenuForUpdateAsync(menuId);
        if (menu is null)
        {
            return NotFound();
        }

        menu.DisplayText = name;
        menu.Alter<ContentFieldSet>("Title", title => title.Alter<Grove.Contents.Fields.Fields.TextField>("Title", text => text.Text = name));
        return await SaveAsync(menu);
    }

    public async Task<IResult> ToggleMenuAsync(string menuId)
    {
        if (!await IsAuthorizedAsync())
        {
            return Forbid();
        }

        var menu = await contentManager.GetAsync(menuId, VersionOptions.Latest);
        if (menu is null)
        {
            return NotFound();
        }

        if (menu.Published)
        {
            await contentManager.UnpublishAsync(menu);
            menu = await contentManager.GetAsync(menuId, VersionOptions.Latest) ?? menu;
        }
        else
        {
            await contentManager.PublishAsync(menu);
        }

        return Ok(StandardMenuSummary.From(menu));
    }

    public async Task<IResult> DuplicateMenuAsync(string menuId)
    {
        if (!await IsAuthorizedAsync())
        {
            return Forbid();
        }

        var source = await contentManager.GetAsync(menuId, VersionOptions.Latest);
        if (source is null)
        {
            return NotFound();
        }

        var copy = await contentManager.NewAsync("Menu");
        copy.DisplayText = $"{source.DisplayText} Copy";
        copy.Content.Merge(CloneObject(source.Content));
        copy.ContentItemId = string.Empty;
        copy.ContentItemVersionId = string.Empty;
        RegenerateNodeIds(copy.Content);
        await contentManager.CreateAsync(copy, VersionOptions.Published);

        return Ok(StandardMenuSummary.From(copy));
    }

    public async Task<IResult> DeleteMenuAsync(string menuId)
    {
        if (!await IsAuthorizedAsync())
        {
            return Forbid();
        }

        var menu = await contentManager.GetAsync(menuId, VersionOptions.Latest);
        if (menu is null)
        {
            return NotFound();
        }

        await contentManager.RemoveAsync(menu);
        return NoContent();
    }

    // A changed menu is saved like any item: updated (its system flows sanitize the HTML items),
    // validated (every item's address), then published; a refusal answers 400 by field.
    private async Task<IResult> SaveAsync(PlatformContentItem menu)
    {
        await contentManager.UpdateAsync(menu);
        var validation = await contentManager.ValidateAsync(menu);
        if (!validation.Succeeded)
        {
            await HttpContext.RequestServices.GetRequiredService<Grove.Data.Documents.IDocumentStore>().CancelAsync();
            return ApiProblems.Validation(validation.Errors
                .GroupBy(error => error.MemberNames?.FirstOrDefault() ?? string.Empty)
                .ToDictionary(errors => errors.Key, errors => errors.Select(error => error.ErrorMessage ?? string.Empty).ToArray()));
        }

        await contentManager.PublishAsync(menu);
        return Ok(StandardMenuSummary.From(menu));
    }

    public async Task<IResult> CreateNodeAsync(string menuId, StandardMenuNodeEditModel model)
    {
        if (!await IsAuthorizedAsync())
        {
            return Forbid();
        }

        var menu = await LoadMenuForUpdateAsync(menuId);
        if (menu is null)
        {
            return NotFound();
        }

        if (!IsSupportedNodeType(model.Type))
        {
            return BadRequest("Unsupported site menu item type.");
        }

        var node = await CreateNodeObjectAsync(model);
        if (!TryGetChildren((JsonObject)menu.Content, model.ParentNodeId, out var siblings))
        {
            return BadRequest("The selected parent menu item was not found.");
        }

        siblings.Insert(ClampPosition(model.Position, siblings.Count), node);
        return await SaveAsync(menu);
    }

    public async Task<IResult> UpdateNodeAsync(string menuId, string nodeId, StandardMenuNodeEditModel model)
    {
        if (!await IsAuthorizedAsync())
        {
            return Forbid();
        }

        var menu = await LoadMenuForUpdateAsync(menuId);
        if (menu is null)
        {
            return NotFound();
        }

        if (!TryFindNode((JsonObject)menu.Content, nodeId, out var node, out _))
        {
            return NotFound();
        }

        ApplyNode(node, model);
        return await SaveAsync(menu);
    }

    public async Task<IResult> MoveNodeAsync(string menuId, string nodeId, StandardMenuNodeMoveModel model)
    {
        if (!await IsAuthorizedAsync())
        {
            return Forbid();
        }

        var menu = await LoadMenuForUpdateAsync(menuId);
        if (menu is null)
        {
            return NotFound();
        }

        if (!TryFindNode((JsonObject)menu.Content, nodeId, out var node, out var sourceArray) || sourceArray is null)
        {
            return NotFound();
        }

        if (model.ParentNodeId == nodeId || IsDescendant(node, model.ParentNodeId))
        {
            return BadRequest("A menu item cannot be moved under itself or one of its children.");
        }

        if (!TryGetChildren((JsonObject)menu.Content, model.ParentNodeId, out var targetArray))
        {
            return BadRequest("The selected parent menu item was not found.");
        }

        sourceArray.Remove(node);
        targetArray.Insert(ClampPosition(model.Position, targetArray.Count), node);
        return await SaveAsync(menu);
    }

    public async Task<IResult> DuplicateNodeAsync(string menuId, string nodeId)
    {
        if (!await IsAuthorizedAsync())
        {
            return Forbid();
        }

        var menu = await LoadMenuForUpdateAsync(menuId);
        if (menu is null)
        {
            return NotFound();
        }

        if (!TryFindNode((JsonObject)menu.Content, nodeId, out var node, out var siblings) || siblings is null)
        {
            return NotFound();
        }

        var copy = CloneObject(node);
        copy["DisplayText"] = $"{ReadString(node, "DisplayText") ?? "Menu item"} Copy";
        RegenerateNodeIds(copy);
        siblings.Insert(siblings.IndexOf(node) + 1, copy);
        return await SaveAsync(menu);
    }

    public async Task<IResult> DeleteNodeAsync(string menuId, string nodeId)
    {
        if (!await IsAuthorizedAsync())
        {
            return Forbid();
        }

        var menu = await LoadMenuForUpdateAsync(menuId);
        if (menu is null)
        {
            return NotFound();
        }

        if (!TryFindNode((JsonObject)menu.Content, nodeId, out var node, out var siblings) || siblings is null)
        {
            return NotFound();
        }

        siblings.Remove(node);
        return await SaveAsync(menu);
    }

    private Task<bool> IsAuthorizedAsync() => authorizationService.AuthorizeAsync(User, Grove.Contents.SiteMenu.Permissions.ManageMenu);

    private async Task<PlatformContentItem?> LoadMenuForUpdateAsync(string menuId)
    {
        var definition = await contentDefinitionManager.GetTypeDefinitionAsync("Menu");
        return definition?.IsDraftable() == true
            ? await contentManager.GetAsync(menuId, VersionOptions.DraftRequired)
            : await contentManager.GetAsync(menuId, VersionOptions.Latest);
    }

    private async Task<StandardMenuNodeType[]> GetAvailableNodeTypesAsync()
    {
        var definitions = await contentDefinitionManager.ListTypeDefinitionsAsync();
        return definitions
            .Where(type => type.GetStereotype() == "MenuItem" && IsSupportedNodeType(type.Name))
            .Select(type => new StandardMenuNodeType(type.Name, string.IsNullOrWhiteSpace(type.DisplayName) ? type.Name : type.DisplayName))
            .OrderBy(type => type.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static bool IsSupportedNodeType(string? type) =>
        string.Equals(type, "LinkMenuItem", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(type, "HtmlMenuItem", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(type, "ContentMenuItem", StringComparison.OrdinalIgnoreCase);

    private async Task<JsonObject> CreateNodeObjectAsync(StandardMenuNodeEditModel model)
    {
        var contentItem = await contentManager.NewAsync(model.Type);
        contentItem.DisplayText = model.Text?.Trim() ?? string.Empty;
        ApplyNode(contentItem.Content, model);
        EnsureMenuItems(contentItem.Content);
        var node = JsonSerializer.SerializeToNode(contentItem)?.AsObject() ?? [];
        EnsureMenuItems(node);
        return node;
    }

    private static void ApplyNode(JsonObject node, StandardMenuNodeEditModel model)
    {
        node["DisplayText"] = model.Text?.Trim() ?? string.Empty;
        var type = ReadString(node, "ContentType") ?? model.Type;

        if (string.Equals(type, "ContentMenuItem", StringComparison.OrdinalIgnoreCase))
        {
            // A content menu item links to a content item, chosen by id (the MVC menu admin's
            // ContentMenuItemFieldSet editor, ported).
            node["ContentMenuItem"] = new JsonObject
            {
                ["CheckContentPermissions"] = new JsonObject { ["Value"] = model.CheckContentPermissions },
                ["SelectedContentItem"] = new JsonObject
                {
                    ["SourceKey"] = "contentitem:*",
                    ["SelectedIds"] = string.IsNullOrWhiteSpace(model.ContentItemId) ? new JsonArray() : new JsonArray(JsonValue.Create(model.ContentItemId.Trim())),
                },
            };
        }
        else if (string.Equals(type, "HtmlMenuItem", StringComparison.OrdinalIgnoreCase))
        {
            node["HtmlMenuItem"] = new JsonObject
            {
                [nameof(HtmlMenuItemFieldSet.Url)] = new JsonObject { ["Text"] = model.Url?.Trim() ?? string.Empty },
                [nameof(HtmlMenuItemFieldSet.Target)] = new JsonObject { ["Text"] = model.Target?.Trim() ?? string.Empty },
                [nameof(HtmlMenuItemFieldSet.Html)] = new JsonObject { ["Html"] = model.Html?.Trim() ?? string.Empty },
            };
        }
        else
        {
            node["LinkMenuItem"] = new JsonObject
            {
                [nameof(LinkMenuItemFieldSet.Url)] = new JsonObject { ["Text"] = model.Url?.Trim() ?? string.Empty },
                [nameof(LinkMenuItemFieldSet.Target)] = new JsonObject { ["Text"] = model.Target?.Trim() ?? string.Empty },
            };
        }

        if (model.PermissionNames is { Length: > 0 })
        {
            node["MenuItemPermission"] = new JsonObject { ["PermissionNames"] = new JsonObject { ["Values"] = new JsonArray(model.PermissionNames.Select(name => JsonValue.Create(name)).ToArray()) } };
        }
        else
        {
            node.Remove("MenuItemPermission");
        }

        EnsureMenuItems(node);
    }

    private static bool TryGetChildren(JsonObject menuOrNode, string? parentNodeId, out JsonArray children)
    {
        if (string.IsNullOrWhiteSpace(parentNodeId))
        {
            children = EnsureMenuItems(menuOrNode);
            return true;
        }

        if (TryFindNode(menuOrNode, parentNodeId, out var parent, out _))
        {
            children = EnsureMenuItems(parent);
            return true;
        }

        children = [];
        return false;
    }

    private static bool TryFindNode(JsonObject source, string nodeId, out JsonObject node, out JsonArray? siblings)
    {
        foreach (var child in EnsureMenuItems(source).OfType<JsonObject>())
        {
            if (string.Equals(ReadString(child, "ContentItemId"), nodeId, StringComparison.OrdinalIgnoreCase))
            {
                node = child;
                siblings = EnsureMenuItems(source);
                return true;
            }

            if (TryFindNode(child, nodeId, out node, out siblings))
            {
                return true;
            }
        }

        node = [];
        siblings = null;
        return false;
    }

    private static bool IsDescendant(JsonObject node, string? possibleDescendantId)
    {
        if (string.IsNullOrWhiteSpace(possibleDescendantId))
        {
            return false;
        }

        foreach (var child in EnsureMenuItems(node).OfType<JsonObject>())
        {
            if (string.Equals(ReadString(child, "ContentItemId"), possibleDescendantId, StringComparison.OrdinalIgnoreCase) || IsDescendant(child, possibleDescendantId))
            {
                return true;
            }
        }

        return false;
    }

    // The items under a menu or a menu item: its MenuItems embedding.
    private static JsonArray EnsureMenuItems(JsonObject source)
    {
        if (source[MenuItemsEmbedding.Name] is not JsonObject embedding)
        {
            source[MenuItemsEmbedding.Name] = embedding = [];
        }

        if (embedding["ContentItems"] is not JsonArray items)
        {
            embedding["ContentItems"] = items = [];
        }

        return items;
    }

    private static void RegenerateNodeIds(JsonObject source)
    {
        source["ContentItemId"] = Guid.NewGuid().ToString("n");
        source["ContentItemVersionId"] = Guid.NewGuid().ToString("n");
        foreach (var child in EnsureMenuItems(source).OfType<JsonObject>())
        {
            RegenerateNodeIds(child);
        }
    }

    private static JsonObject CloneObject(JsonObject source) => source.DeepClone().AsObject();

    private static int ClampPosition(int? position, int count) => Math.Clamp(position ?? count, 0, count);

    private static string? ReadString(JsonObject source, string propertyName) => source[propertyName]?.GetValue<string>();
}
