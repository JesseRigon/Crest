using Grove.Contents.Management.Handlers;
using Grove.Contents.Management.Routing;
using Grove.Modules.Services;

namespace Grove.Routes.Auto.Projections;

/// <summary>The route of an item no field set routes (a read projection): its id and display text, slugified.</summary>
public sealed class DefaultRouteProjection : IContentProjection
{
    private readonly ISlugService _slugService;

    public DefaultRouteProjection(ISlugService slugService)
    {
        _slugService = slugService;
    }

    public Task ProjectAsync(ContentItemAspectContext context)
    {
        return context.ForAsync<RouteHandlerAspect>(aspect =>
        {
            // Only use default aspect if no other handler has set the aspect.
            if (string.IsNullOrEmpty(aspect.Path))
            {
                // By default contained route is content item id + display text, if present.
                var path = context.ContentItem.ContentItemId;
                if (!string.IsNullOrEmpty(context.ContentItem.DisplayText))
                {
                    path = path + "-" + context.ContentItem.DisplayText;
                }

                aspect.Path = _slugService.Slugify(path);
            }

            return Task.CompletedTask;
        });
    }
}
