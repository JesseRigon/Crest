using Crest.Workflows.Http.Contexts;

namespace Crest.Workflows.Http;

public interface IHttpEndpointRoutesProvider
{
    Task<IEnumerable<HttpRouteData>> GetRoutesAsync(HttpEndpointRouteProviderContext context);
}