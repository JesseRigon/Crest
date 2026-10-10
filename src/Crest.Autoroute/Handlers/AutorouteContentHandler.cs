using Microsoft.AspNetCore.Routing;
using Crest.ContentManagement;
using Crest.ContentManagement.Handlers;
using Crest.ContentManagement.Routing;

namespace Crest.Autoroute.Handlers;

public class AutorouteContentHandler : ContentHandlerBase
{
    private readonly IAutorouteEntries _autorouteEntries;

    public AutorouteContentHandler(IAutorouteEntries autorouteEntries)
    {
        _autorouteEntries = autorouteEntries;
    }

    public override Task GetContentItemAspectAsync(ContentItemAspectContext context)
    {
        return context.ForAsync<ContentItemMetadata>(async metadata =>
        {
            // When a content item is contained we provide different route values when generating urls.
            (var found, var entry) = await _autorouteEntries.TryGetEntryByContentItemIdAsync(context.ContentItem.ContentItemId);

            if (found && !string.IsNullOrEmpty(entry.ContainedContentItemId))
            {
                metadata.DisplayRouteValues = new RouteValueDictionary {
                    { "Area", "Crest.Contents" },
                    { "Controller", "Item" },
                    { "Action", "Display" },
                    { "ContentItemId", entry.ContentItemId},
                    { "ContainedContentItemId", entry.ContainedContentItemId },
                };
            }
        });
    }
}
