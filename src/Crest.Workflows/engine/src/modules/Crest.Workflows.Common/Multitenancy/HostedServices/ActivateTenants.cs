using JetBrains.Annotations;
using Microsoft.Extensions.Hosting;

namespace Crest.Workflows.Common.Multitenancy.HostedServices;

[UsedImplicitly]
public class ActivateTenants(ITenantService tenantService) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        await tenantService.ActivateTenantsAsync(cancellationToken);
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        await tenantService.DeactivateTenantsAsync(cancellationToken);
    }
}