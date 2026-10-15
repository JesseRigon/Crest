using Grove.Workflows.Extensions;
using Grove.Workflows.Http.Bookmarks;
using Grove.Workflows.Http.Contexts;
using Grove.Workflows.Runtime;
using Grove.Workflows.Runtime.Filters;

namespace Grove.Workflows.Http.Services;

/// <inheritdoc />
public class HttpEndpointRoutes(
    ITriggerStore triggerStore,
    IBookmarkStore bookmarkStore,
    IHttpEndpointRoutesProvider httpEndpointRoutesProvider) : IHttpEndpointRoutes
{
    public async Task<IReadOnlyList<HttpRouteData>> ListAsync(CancellationToken cancellationToken = default)
    {
        var stimulusName = HttpStimulusNames.HttpEndpoint;
        var triggers = await triggerStore.FindManyAsync(new TriggerFilter { Name = stimulusName }, cancellationToken);
        var bookmarks = await bookmarkStore.FindManyAsync(new BookmarkFilter { Name = stimulusName }, cancellationToken);

        var routes = new Dictionary<string, HttpRouteData>(StringComparer.OrdinalIgnoreCase);
        var payloads = triggers.Where(trigger => trigger.Payload != null).Select(trigger => (Payload: trigger.GetPayload<HttpEndpointBookmarkPayload>(), trigger.TenantId))
            .Concat(bookmarks.Where(bookmark => bookmark.Payload != null).Select(bookmark => (Payload: bookmark.GetPayload<HttpEndpointBookmarkPayload>(), bookmark.TenantId)));

        foreach (var (payload, tenantId) in payloads)
        {
            var context = new HttpEndpointRouteProviderContext(payload, tenantId, cancellationToken);
            foreach (var route in await httpEndpointRoutesProvider.GetRoutesAsync(context))
            {
                if (route.Route.Contains("//"))
                    continue;

                routes.TryAdd(route.Route.NormalizeRoute(), route);
            }
        }

        return routes.Values.ToList();
    }
}
