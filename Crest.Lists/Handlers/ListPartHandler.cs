using Microsoft.AspNetCore.Routing;
using Crest.ContentManagement;
using Crest.ContentManagement.Handlers;
using Crest.Lists.Models;

namespace Crest.Lists.Drivers;

public class ListPartHandler : ContentPartHandler<ListPart>
{
    public override Task GetContentItemAspectAsync(ContentItemAspectContext context, ListPart part)
    {
        return context.ForAsync<ContentItemMetadata>(contentItemMetadata =>
        {
            contentItemMetadata.AdminRouteValues = new RouteValueDictionary
            {
                {"Area", "Crest.Contents"},
                {"Controller", "Admin"},
                {"Action", "Display"},
                {"ContentItemId", context.ContentItem.ContentItemId},
            };

            return Task.CompletedTask;
        });
    }
}
