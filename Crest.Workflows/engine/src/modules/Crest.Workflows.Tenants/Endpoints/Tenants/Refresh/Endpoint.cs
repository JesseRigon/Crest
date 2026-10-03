using Crest.Workflows.Abstractions;
using Crest.Workflows.Common.Multitenancy;

namespace Crest.Workflows.Tenants.Endpoints.Tenants.Refresh;

public class Endpoint(ITenantService tenantService) : CrestWorkflowsEndpointWithoutRequest
{
    public override void Configure()
    {
        Post("/tenants/refresh");
        ConfigurePermissions("execute:tenants:refresh");
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        await tenantService.RefreshAsync(ct);
        await Send.OkAsync(cancellation: ct);
    }
}