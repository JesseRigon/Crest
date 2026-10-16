using System.Text.Json.Nodes;
using Grove.Contents.Management.Handlers;
using Grove.Contents.Management.Routing;
using Grove.Contents.Taxonomies.Models;

namespace Grove.Contents.Taxonomies.Projections;

/// <summary>A term's child terms (a read projection): the embedded-items accessor of the taxonomy's nested Terms embedding under a term.</summary>
public sealed class TermChildrenProjection : IContentProjection
{
    public Task ProjectAsync(ContentItemAspectContext context)
    {
        return context.ForAsync<EmbeddedContentItemsAspect>(aspect =>
        {
            // A term holds its child terms under the taxonomy's nested Terms embedding, which its own
            // type does not declare: the term answers for them (referenceable, as the taxonomy's are).
            if (TaxonomyFieldSet.TermsArray((JsonNode)context.ContentItem.Content) is not null)
            {
                Func<JsonObject, JsonArray> accessor = jsonObject => TaxonomyFieldSet.TermsArray(jsonObject);
                aspect.Accessors.Add(accessor);
                aspect.ReferenceableAccessors.Add(accessor);
            }

            return Task.CompletedTask;
        });
    }
}
