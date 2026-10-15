using System.Text.Json.Nodes;
using Grove.Contents.Management.Handlers;
using Grove.Contents.Management.Routing;
using Grove.Contents.Taxonomies.Models;

namespace Grove.Contents.Taxonomies.Handlers;

public class TaxonomyPartHandler : ContentPartHandler<TaxonomyPart>
{
    public override Task GetContentItemAspectAsync(ContentItemAspectContext context, TaxonomyPart part)
    {
        return context.ForAsync<ContainedContentItemsAspect>(aspect =>
        {
            aspect.Accessors.Add((jsonObject) =>
            {
                return jsonObject["TaxonomyPart"]["Terms"] as JsonArray;
            });

            return Task.CompletedTask;
        });
    }

    // The terms belong to the taxonomy whatever editor wrote them: each term's TermPart names it
    // (a term added in the native editor may not know the id of a taxonomy not saved yet).
    public override Task CreatingAsync(CreateContentContext context, TaxonomyPart part) => OwnTermsAsync(context.ContentItem);

    public override Task UpdatingAsync(UpdateContentContext context, TaxonomyPart part) => OwnTermsAsync(context.ContentItem);

    private static Task OwnTermsAsync(Grove.Contents.Management.ContentItem taxonomy)
    {
        if (((JsonObject)taxonomy.Content)["TaxonomyPart"]?["Terms"] is JsonArray terms)
        {
            Own(terms, taxonomy.ContentItemId);
        }

        return Task.CompletedTask;
    }

    private static void Own(JsonArray terms, string taxonomyId)
    {
        foreach (var term in terms.OfType<JsonObject>())
        {
            if (term["TermPart"] is not JsonObject termPart)
            {
                term["TermPart"] = termPart = [];
            }

            termPart["TaxonomyContentItemId"] = taxonomyId;
            if (term["Terms"] is JsonArray children)
            {
                Own(children, taxonomyId);
            }
        }
    }

    // A taxonomy names the type its terms are created as.
    protected override Task ValidatingAsync(ValidateContentPartContext context, TaxonomyPart part)
    {
        if (string.IsNullOrWhiteSpace(part.TermContentType))
        {
            context.Fail("The term content type is required.", nameof(part.TermContentType));
        }

        return Task.CompletedTask;
    }
}
