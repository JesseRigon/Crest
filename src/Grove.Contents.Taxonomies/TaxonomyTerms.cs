using System.Text.Json.Nodes;
using Grove.Contents.Management;

namespace Grove.Contents.Taxonomies;

/// <summary>Finds terms in a taxonomy's term tree (<c>TaxonomyPart.Terms</c>), for the index, GraphQL and Liquid.</summary>
internal static class TaxonomyTerms
{
    /// <summary>The term <paramref name="termContentItemId"/>, at any depth, or null.</summary>
    internal static ContentItem FindTerm(JsonArray termsArray, string termContentItemId)
    {
        foreach (var term in termsArray.Cast<JsonObject>())
        {
            var contentItemId = term["ContentItemId"]?.ToString();

            if (contentItemId == termContentItemId)
            {
                return term.ToObject<ContentItem>();
            }

            if (term["Terms"] is JsonArray children)
            {
                var found = FindTerm(children, termContentItemId);

                if (found != null)
                {
                    return found;
                }
            }
        }

        return null;
    }

    /// <summary>Adds the term <paramref name="termContentItemId"/> and its ancestors to <paramref name="terms"/>, the term first; false when it is not in the tree.</summary>
    internal static bool FindTermHierarchy(JsonArray termsArray, string termContentItemId, List<ContentItem> terms)
    {
        foreach (var term in termsArray.Cast<JsonObject>())
        {
            var contentItemId = term["ContentItemId"]?.ToString();

            if (contentItemId == termContentItemId)
            {
                terms.Add(term.ToObject<ContentItem>());

                return true;
            }

            if (term["Terms"] is JsonArray children)
            {
                var found = FindTermHierarchy(children, termContentItemId, terms);

                if (found)
                {
                    terms.Add(term.ToObject<ContentItem>());

                    return true;
                }
            }
        }

        return false;
    }
}
