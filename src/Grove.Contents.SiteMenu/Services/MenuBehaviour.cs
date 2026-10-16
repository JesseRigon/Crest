using System.Text.Encodings.Web;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Localization;
using Grove.Contents.Management;
using Grove.Contents.Management.Handlers;
using Grove.Contents.Management.Metadata.Models;
using Grove.Infrastructure.Html;
using Grove.Contents.SiteMenu.Models;
using Grove.Contents.SiteMenu.Settings;

namespace Grove.Contents.SiteMenu.Services;

/// <summary>
/// What the menu's system flows do (<see cref="Workflows.MenuWorkflowProvider"/>). A menu's items are
/// embedded content items without a lifecycle of their own, so the menu's flows check and clean them
/// on the menu's saves: every link and HTML item's address is a well-formed, safe URL, and an HTML
/// item's markup is sanitized when its type's field set settings say so. Each item is read through
/// the definition its embedding gives it (<see cref="IEmbeddedContentReader"/>).
/// </summary>
public sealed class MenuBehaviour(
    IEmbeddedContentReader embeddedContentReader,
    IHtmlSanitizerService sanitizer,
    HtmlEncoder encoder,
    IStringLocalizer<MenuBehaviour> S)
{
    /// <summary>The refusal of a menu holding an item with an invalid address; null when every address is fine.</summary>
    public async Task<ContentFieldRefusal> AddressRefusalAsync(ContentItem menu)
    {
        foreach (var entry in await embeddedContentReader.ReadAsync(menu, MenuItemsEmbedding.Name))
        {
            var url = FieldSet<LinkMenuItemFieldSet>(entry)?.Url ?? FieldSet<HtmlMenuItemFieldSet>(entry)?.Url;
            if (!IsValidUrl(url))
            {
                return new ContentFieldRefusal(S["{0} is an invalid url.", url], MenuItemsEmbedding.Name);
            }
        }

        return null;
    }

    /// <summary>Sanitizes the markup of the menu's HTML items whose type's field set settings say so.</summary>
    public async Task SanitizeAsync(ContentItem menu)
    {
        var sanitized = new Dictionary<string, (string FieldSet, string Html)>(StringComparer.Ordinal);
        foreach (var entry in await embeddedContentReader.ReadAsync(menu, MenuItemsEmbedding.Name))
        {
            var typeFieldSet = entry.Definition.FieldSets
                .FirstOrDefault(x => x.FieldSetDefinition.Name == ContentFieldSetName.Of<HtmlMenuItemFieldSet>());
            if (typeFieldSet is null
                || !(typeFieldSet.GetSettings<HtmlMenuItemFieldSetSettings>() ?? new HtmlMenuItemFieldSetSettings()).SanitizeHtml
                || entry.Item.Get<HtmlMenuItemFieldSet>(typeFieldSet.Name)?.Html is not { Length: > 0 } html
                || string.IsNullOrEmpty(entry.Item.ContentItemId))
            {
                continue;
            }

            sanitized[entry.Item.ContentItemId] = (typeFieldSet.Name, sanitizer.Sanitize(html));
        }

        if (sanitized.Count > 0)
        {
            Write(MenuItemsEmbedding.ItemsArray(menu.Content), sanitized);
        }
    }

    // The embedded items are copies; the sanitized markup is written into the menu's own content.
    private static void Write(JsonArray items, Dictionary<string, (string FieldSet, string Html)> sanitized)
    {
        foreach (var node in items?.OfType<JsonObject>() ?? [])
        {
            if (node["ContentItemId"]?.GetValue<string>() is { } id
                && sanitized.TryGetValue(id, out var value)
                && node[value.FieldSet] is JsonObject fieldSet)
            {
                fieldSet[nameof(HtmlMenuItemFieldSet.Html)] = new JsonObject { ["Html"] = value.Html };
            }

            Write(MenuItemsEmbedding.ItemsArray(node), sanitized);
        }
    }

    private static T FieldSet<T>(EmbeddedContentEntry entry) where T : ContentFieldSet
    {
        var typeFieldSet = entry.Definition.FieldSets.FirstOrDefault(x => x.FieldSetDefinition.Name == ContentFieldSetName.Of<T>());
        return typeFieldSet is null ? null : entry.Item.Get<T>(typeFieldSet.Name);
    }

    /// <summary>A menu item's address is well formed and survives sanitizing as a link; an empty address is fine.</summary>
    public bool IsValidUrl(string url)
    {
        if (string.IsNullOrEmpty(url))
        {
            return true;
        }

        var toValidate = url.Split('#', 2)[0];
        if (toValidate.StartsWith("~/", StringComparison.Ordinal))
        {
            toValidate = toValidate[1..];
        }

        toValidate = toValidate.ToUriComponents();
        if (!Uri.IsWellFormedUriString(toValidate, UriKind.RelativeOrAbsolute))
        {
            return false;
        }

        var link = $"<a href=\"{encoder.Encode(toValidate)}\"></a>";
        return string.Equals(link, sanitizer.Sanitize(link), StringComparison.OrdinalIgnoreCase);
    }
}
