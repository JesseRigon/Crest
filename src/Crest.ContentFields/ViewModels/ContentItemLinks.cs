using Crest.ContentManagement;

namespace Crest.ContentFields.ViewModels;

/// <summary>
/// A content item's link on the site, relative to the shell's base: its permalink when it has
/// one, else the item route the site resolves (docs/blazor-display.md rework step 3).
/// </summary>
public static class ContentItemLinks
{
    public static string SitePath(ContentItem item)
    {
        System.Text.Json.Nodes.JsonObject content = item.Content;
        return content["AutoroutePart"]?["Path"] is System.Text.Json.Nodes.JsonValue value && value.TryGetValue<string>(out var path) && path.Length > 0
            ? path.TrimStart('/')
            : "Contents/ContentItems/" + Uri.EscapeDataString(item.ContentItemId);
    }
}
