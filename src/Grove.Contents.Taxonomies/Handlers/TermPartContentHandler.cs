using System.Text.Json.Nodes;
using Grove.Contents.Management.Handlers;
using Grove.Contents.Management.Routing;

namespace Grove.Contents.Taxonomies.Handlers;

public class TermPartContentHandler : ContentHandlerBase
{
    public override Task GetContentItemAspectAsync(ContentItemAspectContext context)
    {
        return context.ForAsync<ContainedContentItemsAspect>(aspect =>
        {
            // Check this content item contains Terms.
            if (((JsonNode)context.ContentItem.Content)["Terms"] is JsonArray)
            {
                aspect.Accessors.Add((jsonObject) =>
                {
                    return jsonObject["Terms"] as JsonArray;
                });
            }

            return Task.CompletedTask;
        });
    }
}
