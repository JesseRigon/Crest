using System.Text.Json.Nodes;
using Grove.Contents.Management;

namespace Grove.Contents.Taxonomies;

/// <summary>Finds terms in a taxonomy's term tree (its <c>Terms</c> embedding, each term's children under its own), for the index, GraphQL and Liquid.</summary>
internal static class TaxonomyTerms
{
    /// <summary>The terms directly under a taxonomy (its content item).</summary>
    internal static JsonArray Roots(ContentItem taxonomy) => Models.TaxonomyFieldSet.TermsArray((JsonObject)taxonomy.Content) ?? [];

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

            if (Models.TaxonomyFieldSet.TermsArray(term) is { } children)
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

            if (Models.TaxonomyFieldSet.TermsArray(term) is { } children)
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
