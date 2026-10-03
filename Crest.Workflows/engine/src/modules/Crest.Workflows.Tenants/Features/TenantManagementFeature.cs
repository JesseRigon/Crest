using Crest.Workflows.Common.Multitenancy;
using Crest.Workflows.Extensions;
using Crest.Workflows.Features.Abstractions;
using Crest.Workflows.Features.Services;
using Microsoft.Extensions.DependencyInjection;

namespace Crest.Workflows.Tenants.Features;

/// <summary>
/// Enables tenant management endpoints.
/// </summary>
public class TenantManagementFeature(IModule serviceConfiguration) : FeatureBase(serviceConfiguration)
{
    private Func<IServiceProvider, ITenantStore> _tenantStoreFactory = sp => sp.GetRequiredService<MemoryTenantStore>();

    public TenantManagementFeature WithTenantStore(Func<IServiceProvider, ITenantStore> factory)
    {
        _tenantStoreFactory = factory;
        return this;
    }

    /// <inheritdoc />
    public override void Apply()
    {
        Services
            .AddMemoryStore<Tenant, MemoryTenantStore>()
            .AddScoped(_tenantStoreFactory);
    }
}