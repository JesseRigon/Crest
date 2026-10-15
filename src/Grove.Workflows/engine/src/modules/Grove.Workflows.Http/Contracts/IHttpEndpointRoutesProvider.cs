using Grove.Workflows.Http.Contexts;

namespace Grove.Workflows.Http;

public interface IHttpEndpointRoutesProvider
{
    Task<IEnumerable<HttpRouteData>> GetRoutesAsync(HttpEndpointRouteProviderContext context);
}