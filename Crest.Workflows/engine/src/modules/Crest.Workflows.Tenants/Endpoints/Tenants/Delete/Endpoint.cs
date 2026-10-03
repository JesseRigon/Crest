using Crest.Workflows.Abstractions;
using Crest.Workflows.Common.Multitenancy;

namespace Crest.Workflows.Tenants.Endpoints.Tenants.Delete;

public class Endpoint(ITenantService tenantService, ITenantStore store) : CrestWorkflowsEndpointWithoutRequest<Tenant>
{
    public override void Configure()
    {
        Delete("/tenants/{id}");
        ConfigurePermissions("delete:tenants");
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var id = Route<string>("id")!;
        var found = await store.DeleteAsync(id, ct);
        
        if (!found)
        {
            await Send.NotFoundAsync(ct);
            return;
        }
        
        await tenantService.RefreshAsync(ct);
        await Send.OkAsync(cancellation: ct);
    }
}