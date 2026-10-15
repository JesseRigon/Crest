using System.Text.Json;
using System.Text.Json.Nodes;
using Grove.Contents.Management;

namespace Grove.Contents.Taxonomies.ViewModels;

/// <summary>A term in a taxonomy's tree as its display shows it: text, link and child terms.</summary>
public sealed record TaxonomyTermNode(string Text, string Url, IReadOnlyList<TaxonomyTermNode> Children);

/// <summary>Terms' links on the site, relative to the shell's base (docs/blazor-display.md rework step 7).</summary>
public static class TaxonomyLinks
{
    /// <summary>A term's permalink (the taxonomy routes its contained terms), else its item route.</summary>
    public static string TermPath(ContentItem term)
    {
        JsonObject content = term.Content;
        return content["AutoroutePart"]?["Path"] is JsonValue value && value.TryGetValue<string>(out var path) && path.Length > 0
            ? path.TrimStart('/')
            : "Contents/ContentItems/" + Uri.EscapeDataString(term.ContentItemId);
    }

    /// <summary>The term tree under <paramref name="terms"/>, depth first.</summary>
    public static IReadOnlyList<TaxonomyTermNode> Tree(IEnumerable<ContentItem> terms) =>
        [.. (terms ?? []).Select(term =>
        {
            JsonObject content = term.Content;
            var children = content["Terms"] is JsonArray nested ? nested.Deserialize<List<ContentItem>>() ?? [] : [];
            return new TaxonomyTermNode(term.DisplayText ?? term.ContentItemId, TermPath(term), Tree(children));
        })];
}
