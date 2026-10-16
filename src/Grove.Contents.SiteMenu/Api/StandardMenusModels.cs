#nullable enable

using System.Text.Json;
using Grove.Validation;
using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Grove;
using Grove.Contents.Management;
using Grove.Contents.Management.Metadata;
using Grove.Contents.Management.Metadata.Models;
using Grove.Contents;
using Grove.Contents.SiteMenu;
using Grove.Contents.SiteMenu.Models;
using PlatformContentItem = Grove.Contents.Management.ContentItem;

namespace Grove.Contents.SiteMenu.Api;

public sealed record StandardMenusState(StandardMenuSummary[] Menus, StandardMenuNodeType[] AvailableNodeTypes);

public sealed record StandardMenuNodeType(string Type, string DisplayName);

public sealed record StandardMenuEditModel([property: NotEmpty] string? Name, bool Published);

public sealed record StandardMenuNodeEditModel(
    string Type,
    string Text,
    string? Url,
    string? Target,
    string? Html,
    string[]? PermissionNames,
    string? ParentNodeId,
    int? Position,
    string? ContentItemId = null,
    bool CheckContentPermissions = false);

public sealed record StandardMenuNodeMoveModel(string? ParentNodeId, int? Position);

public sealed record StandardMenuSummary(
    string Id,
    string ContentItemId,
    string ContentItemVersionId,
    string Name,
    bool Published,
    StandardMenuNodeSummary[] Nodes)
{
    public static StandardMenuSummary From(PlatformContentItem menu) => new(
        menu.ContentItemId,
        menu.ContentItemId,
        menu.ContentItemVersionId,
        string.IsNullOrWhiteSpace(menu.DisplayText) ? menu.ContentItemId : menu.DisplayText,
        menu.Published,
        ReadChildren((JsonObject)menu.Content).Select((node, index) => StandardMenuNodeSummary.From(node, null, 0, index)).ToArray());

    internal static IEnumerable<JsonObject> ReadChildren(JsonObject source)
    {
        if (MenuItemsEmbedding.ItemsArray(source) is not { } items)
        {
            yield break;
        }

        foreach (var item in items.OfType<JsonObject>())
        {
            yield return item;
        }
    }
}

public sealed record StandardMenuNodeSummary(
    string Id,
    string Type,
    string Text,
    string? Url,
    string? Target,
    string? Html,
    bool Enabled,
    int Depth,
    int Order,
    string? ParentId,
    string[] PermissionNames,
    StandardMenuNodeSummary[] Items)
{
    /// <summary>The item a content menu item links to.</summary>
    public string? ContentItemId { get; init; }

    public bool CheckContentPermissions { get; init; }

    public static StandardMenuNodeSummary From(JsonObject item, string? parentId, int depth, int order)
    {
        var id = ReadString(item, "ContentItemId") ?? $"menu-node-{depth}-{order}";
        var type = ReadString(item, "ContentType") ?? "MenuItem";
        var text = ReadString(item, "DisplayText") ?? type;
        // A menu item's values are fields: a text field's text, an HTML field's markup, a multi-text field's values.
        var url = ReadString(item, "LinkMenuItem", "Url", "Text") ?? ReadString(item, "HtmlMenuItem", "Url", "Text");
        var target = ReadString(item, "LinkMenuItem", "Target", "Text") ?? ReadString(item, "HtmlMenuItem", "Target", "Text");
        var html = ReadString(item, "HtmlMenuItem", "Html", "Html");
        var permissions = ReadStringArray(item, "MenuItemPermission", "PermissionNames");
        var children = StandardMenuSummary.ReadChildren(item)
            .Select((child, index) => From(child, id, depth + 1, index))
            .ToArray();

        var contentFieldSet = item["ContentMenuItem"] as JsonObject;
        return new(id, type, text, url, target, html, true, depth, order, parentId, permissions, children)
        {
            ContentItemId = (contentFieldSet?["SelectedContentItem"]?["SelectedIds"] as JsonArray)?.FirstOrDefault()?.GetValue<string>(),
            CheckContentPermissions = contentFieldSet?["CheckContentPermissions"]?["Value"] is JsonValue check && check.TryGetValue<bool>(out var value) && value,
        };
    }

    private static string? ReadString(JsonObject source, string propertyName) =>
        source[propertyName]?.GetValue<string>();

    private static string? ReadString(JsonObject source, string partName, string fieldName, string valueName) =>
        source[partName] is JsonObject part ? part[fieldName]?[valueName]?.GetValue<string>() : null;

    private static string[] ReadStringArray(JsonObject source, string partName, string propertyName)
    {
        if (source[partName] is not JsonObject part || part[propertyName]?["Values"] is not JsonArray values)
        {
            return [];
        }

        return values.Select(value => value?.GetValue<string>())
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value!)
            .ToArray();
    }
}
