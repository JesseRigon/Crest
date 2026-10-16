using Microsoft.AspNetCore.Routing;
using Grove.Contents.Management;
using Grove.Contents.Management.Handlers;
using Grove.Contents.Management.Routing;

namespace Grove.Routes.Auto.Projections;

/// <summary>A contained item's route values (a read projection): its container's entry names it.</summary>
public sealed class ContainedRouteValuesProjection : IContentProjection
{
    private readonly IAutorouteEntries _autorouteEntries;

    public ContainedRouteValuesProjection(IAutorouteEntries autorouteEntries)
    {
        _autorouteEntries = autorouteEntries;
    }

    public Task ProjectAsync(ContentItemAspectContext context)
    {
        return context.ForAsync<ContentItemMetadata>(async metadata =>
        {
            // When a content item is contained we provide different route values when generating urls.
            (var found, var entry) = await _autorouteEntries.TryGetEntryByContentItemIdAsync(context.ContentItem.ContentItemId);

            if (found && !string.IsNullOrEmpty(entry.ContainedContentItemId))
            {
                metadata.DisplayRouteValues = new RouteValueDictionary {
                    { "Area", "Grove.Contents" },
                    { "Controller", "Item" },
                    { "Action", "Display" },
                    { "ContentItemId", entry.ContentItemId},
                    { "ContainedContentItemId", entry.ContainedContentItemId },
                };
            }
        });
    }
}
