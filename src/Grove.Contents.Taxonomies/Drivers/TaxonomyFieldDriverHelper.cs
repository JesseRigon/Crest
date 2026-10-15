using System.Text.Json;
using System.Text.Json.Nodes;
using Grove.Contents.Management;
using Grove.Contents.Taxonomies.Fields;
using Grove.Contents.Taxonomies.ViewModels;

namespace Grove.Contents.Taxonomies.Drivers;

public static class TaxonomyFieldDriverHelper
{
    /// <summary>The terms the field holds, in the field's order, from the taxonomy that owns them, each linked to its page.</summary>
    public static async Task<IReadOnlyList<TaxonomyTermLink>> TermLinksAsync(IContentManager contentManager, TaxonomyField field)
    {
        var taxonomy = string.IsNullOrEmpty(field.TaxonomyContentItemId) ? null : await contentManager.GetAsync(field.TaxonomyContentItemId);
        var terms = new Dictionary<string, ContentItem>(StringComparer.Ordinal);
        if (taxonomy?.As<Grove.Contents.Taxonomies.Models.TaxonomyPart>() is { } part)
        {
            Collect(part.Terms, terms);
        }

        return [.. (field.TermContentItemIds ?? [])
            .Where(terms.ContainsKey)
            .Select(id => new TaxonomyTermLink(terms[id].DisplayText ?? id, TaxonomyLinks.TermPath(terms[id])))];
    }

    private static void Collect(IEnumerable<ContentItem> terms, Dictionary<string, ContentItem> into)
    {
        foreach (var term in terms ?? [])
        {
            into[term.ContentItemId] = term;
            System.Text.Json.Nodes.JsonObject content = term.Content;
            if (content["Terms"] is System.Text.Json.Nodes.JsonArray children)
            {
                Collect(children.Deserialize<List<ContentItem>>() ?? [], into);
            }
        }
    }
}
