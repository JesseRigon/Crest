using Crest.Workflows.Abstractions;
using Crest.Workflows.Models;
using Crest.Workflows.Resilience.Serialization;
using Microsoft.AspNetCore.Http;

namespace Crest.Workflows.Resilience.Endpoints.ResilienceStrategies.List;

public class Endpoint(IResilienceStrategyCatalog catalog, ResilienceStrategySerializer serializer) : WorkflowsEndpointWithoutRequest
{
    public override void Configure()
    {
        Get("/resilience/strategies");
        ConfigurePermissions("read:*", "read:resilience", "read:resilience:strategies");
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var strategies = (await catalog.ListAsync(ct)).ToList();
        var response = new ListResponse<IResilienceStrategy>(strategies);

        await HttpContext.Response.WriteAsJsonAsync(response, serializer.SerializerOptions, ct);
    }
}