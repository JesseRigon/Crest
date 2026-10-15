using System.Text.Json.Nodes;
using Grove.ContentManagement.Handlers;
using Grove.ContentManagement.Routing;
using Grove.Taxonomies.Models;

namespace Grove.Taxonomies.Handlers;

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
