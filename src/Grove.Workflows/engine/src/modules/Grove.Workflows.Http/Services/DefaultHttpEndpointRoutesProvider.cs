using Grove.Workflows.Extensions;
using Grove.Workflows.Http.Contexts;
using Grove.Workflows.Http.Options;
using JetBrains.Annotations;
using Microsoft.Extensions.Options;

namespace Grove.Workflows.Http.Services;

/// <summary>
/// Provides a route for an HTTP endpoint based on the specified context.
/// </summary>
[UsedImplicitly]
public class DefaultHttpEndpointRoutesProvider(IOptions<HttpActivityOptions> options) : IHttpEndpointRoutesProvider
{
    public Task<IEnumerable<HttpRouteData>> GetRoutesAsync(HttpEndpointRouteProviderContext context)
    {
        var routes = GetRoutes(context);
        return Task.FromResult(routes);
    }

    private IEnumerable<HttpRouteData> GetRoutes(HttpEndpointRouteProviderContext context)
    {
        var routes = new List<HttpRouteData>();
        var path = context.Payload.Path;

        if (string.IsNullOrWhiteSpace(path))
            return routes;

        var routeData = new HttpRouteData(new[]
        {
            options.Value.BasePath.ToString(), path
        }.JoinSegments());
        routes.Add(routeData);
        return routes;
    }
}