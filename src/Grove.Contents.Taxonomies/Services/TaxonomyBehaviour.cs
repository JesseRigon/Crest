using System.Text.Json.Nodes;
using Grove.Contents.Management;
using Grove.Contents.Management.Handlers;
using Grove.Contents.Taxonomies.Models;

namespace Grove.Contents.Taxonomies.Services;

/// <summary>
/// What the taxonomy's system flows do (<see cref="Workflows.TaxonomyWorkflowProvider"/>): every term,
/// whatever editor wrote it, names its taxonomy; a taxonomy names the type its terms are created as.
/// </summary>
public sealed class TaxonomyBehaviour
{
    /// <summary>Each term's TermFieldSet names the taxonomy (a term added in the editor may not know the id of a taxonomy not saved yet).</summary>
    public Task OwnTermsAsync(ContentItem taxonomy)
    {
        if (TaxonomyFieldSet.TermsArray((JsonObject)taxonomy.Content) is { } terms)
        {
            Own(terms, taxonomy.ContentItemId);
        }

        return Task.CompletedTask;
    }

    /// <summary>The refusal of a taxonomy without a term type; null when it names one.</summary>
    public Task<string> TermTypeErrorAsync(ContentItem taxonomy) =>
        Task.FromResult(!taxonomy.TryGet<TaxonomyFieldSet>(out var part) || string.IsNullOrWhiteSpace(part.TermContentType)
            ? "The term content type is required."
            : null);

    private static void Own(JsonArray terms, string taxonomyId)
    {
        foreach (var term in terms.OfType<JsonObject>())
        {
            if (term["Term"] is not JsonObject termFieldSet)
            {
                term["Term"] = termFieldSet = [];
            }

            termFieldSet[TermFieldSet.TaxonomyContentItemIdField] = new JsonObject { ["Text"] = taxonomyId };
            if (TaxonomyFieldSet.TermsArray(term) is { } children)
            {
                Own(children, taxonomyId);
            }
        }
    }
}
