using System.Text.Json.Nodes;
using Crest.ContentManagement.Handlers;
using Crest.ContentManagement.Routing;
using Crest.Taxonomies.Models;

namespace Crest.Taxonomies.Handlers;

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
}
