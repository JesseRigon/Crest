using System.Net.Mime;
using System.Text.Json;
using Crest.Workflows.Abstractions;
using Crest.Workflows.Common.Multitenancy;
using Crest.Workflows.Common.Serialization;
using Crest.Workflows.Models;

namespace Crest.Workflows.Tenants.Endpoints.Tenants.List;

public class Endpoint(ITenantService tenantService) : CrestWorkflowsEndpointWithoutRequest<ListResponse<Tenant>>
{
    public override void Configure()
    {
        Get("/tenants");
        ConfigurePermissions("read:tenants");
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var tenants = await tenantService.ListAsync(ct);
        var response = new ListResponse<Tenant>(tenants.ToList());
        var json = JsonSerializer.Serialize(response, SerializerOptions.ConfigurationJsonSerializerOptions);
        await Send.StringAsync(json, contentType: MediaTypeNames.Application.Json, cancellation: ct);
    }
}