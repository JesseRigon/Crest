using System.Text.Json.Nodes;
using Grove.Contents.Management;

namespace Grove.Contents.SiteMenu.Models;

/// <summary>
/// A menu's items are its <see cref="Name"/> embedding (docs/content-engine.md › Embedded content
/// items): nested (each item holds its child items under the same name) and referenceable, of the
/// <c>MenuItem</c> stereotype's types.
/// </summary>
public static class MenuItemsEmbedding
{
    public const string Name = "MenuItems";

    /// <summary>The items directly under a menu or a menu item.</summary>
    public static IReadOnlyList<ContentItem> Items(ContentItem menuOrItem) => menuOrItem.Embedded(Name);

    /// <summary>The items array directly under a menu's or a menu item's content; null when there is none.</summary>
    public static JsonArray ItemsArray(JsonNode content) => content?[Name]?["ContentItems"] as JsonArray;
}
