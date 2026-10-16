using System.Text.Json.Nodes;
using Grove.Contents.Management.Handlers;
using Grove.Contents.Management.Routing;
using Grove.Contents.BagParts.Models;

namespace Grove.Contents.BagParts.Handlers;

public class BagPartHandler : ContentPartHandler<BagPart>
{
    public override Task GetContentItemAspectAsync(ContentItemAspectContext context, BagPart part)
    {
        return context.ForAsync<ContainedContentItemsAspect>(aspect =>
        {
            aspect.Accessors.Add((jsonObject) =>
            {
                // Content.Path contains the accessor for named bag parts and typed bag parts.
                var jContent = (JsonObject)part.Content;
                return jsonObject[jContent.GetNormalizedPath()]["ContentItems"] as JsonArray;
            });

            return Task.CompletedTask;
        });
    }
}
