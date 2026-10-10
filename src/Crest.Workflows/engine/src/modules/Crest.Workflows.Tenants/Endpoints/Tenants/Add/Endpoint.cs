using System.Text.Json;
using Crest.Workflows.Abstractions;
using Crest.Workflows.Common.Multitenancy;
using Crest.Workflows.Common.Serialization;
using Crest.Workflows;

namespace Crest.Workflows.Tenants.Endpoints.Tenants.Add;

public class Endpoint(ITenantService tenantService, IIdentityGenerator identityGenerator, ITenantStore tenantStore) : CrestWorkflowsEndpoint<NewTenant, Tenant>
{
    public override void Configure()
    {
        Post("/tenants");
        ConfigurePermissions("write:tenants");
    }

    public override async Task HandleAsync(NewTenant req, CancellationToken ct)
    {
        var tenant = new Tenant
        {
            Id = req.IsDefault ? string.Empty : req.Id ?? identityGenerator.GenerateId(),
            TenantId = req.TenantId,
            Name = req.Name.Trim(),
            Configuration = Serializers.DeserializeConfiguration(req.Configuration)
        };
        await tenantStore.AddAsync(tenant, ct);
        await tenantService.RefreshAsync(ct);
        await Send.OkAsync(tenant, ct);
    }
}

public class NewTenant
{
    public bool IsDefault { get; set; }
    public string? Id { get; set; }
    public string? TenantId { get; set; }
    public string Name { get; set; } = default!;
    public JsonElement? Configuration { get; set; }
}