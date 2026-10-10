using Microsoft.Extensions.DependencyInjection;

namespace Crest.Workflows.Common.Multitenancy;

public class DefaultTenantScopeFactory(ITenantAccessor tenantAccessor, IServiceScopeFactory serviceScopeFactory) : ITenantScopeFactory
{
    public TenantScope CreateScope(Tenant? tenant)
    {
        var serviceScope = serviceScopeFactory.CreateAsyncScope();
        return new(serviceScope, tenantAccessor, tenant);
    }
}