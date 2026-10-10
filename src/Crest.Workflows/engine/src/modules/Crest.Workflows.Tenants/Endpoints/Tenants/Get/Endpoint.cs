using Crest.Workflows.Abstractions;
using Crest.Workflows.Common.Multitenancy;
using Crest.Workflows.Models;

namespace Crest.Workflows.Tenants.Endpoints.Tenants.Get;

public class Endpoint(ITenantService tenantService) : CrestWorkflowsEndpointWithoutRequest<Tenant>
{
    public override void Configure()
    {
        Get("/tenants/{id}");
        ConfigurePermissions("read:tenants");
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var id = Route<string>("id")!;
        var tenant = await tenantService.FindAsync(id, ct);
        
        if (tenant == null)
        {
            await Send.NotFoundAsync(ct);
            return;
        }
        
        await Send.OkAsync(tenant, ct);
    }
}