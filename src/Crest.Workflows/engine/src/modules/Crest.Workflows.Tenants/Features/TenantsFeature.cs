using Crest.Workflows.Common.Features;
using Crest.Workflows.Common.Multitenancy;
using Crest.Workflows.Features.Abstractions;
using Crest.Workflows.Features.Attributes;
using Crest.Workflows.Features.Services;
using Crest.Workflows.Tenants.Mediator.Tasks;
using Crest.Workflows.Tenants.Options;
using Crest.Workflows.Tenants.Providers;
using Microsoft.Extensions.DependencyInjection;

namespace Crest.Workflows.Tenants.Features;

/// <summary>
/// Configures multi-tenancy features.
/// </summary>
[DependencyOf(typeof(MultitenancyFeature))]
public class TenantsFeature(IModule serviceConfiguration) : FeatureBase(serviceConfiguration)
{
    /// <summary>
    /// Configures the Tenants options.
    /// </summary>
    private Action<MultitenancyOptions> MultitenancyOptions { get; set; } = _ => { };
    
    private Action<TenantsOptions> TenantsOptions { get; set; } = _ => { };
    
    public TenantsFeature ConfigureMultitenancy(Action<MultitenancyOptions> configure)
    {
        Services.Configure(configure);
        return this;
    }
    
    public TenantsFeature ConfigureTenants(Action<TenantsOptions> configure)
    {
        Services.Configure(configure);
        return this;
    }
    
    public void UseConfigurationBasedTenantsProvider(Action<TenantsOptions> configure)
    {
        ConfigureTenants(configure);
        Module.Configure<MultitenancyFeature>(feature => feature.UseTenantsProvider<ConfigurationTenantsProvider>());
    }
    
    public void UseStoreBasedTenantsProvider()
    {
        Module.Configure<MultitenancyFeature>(feature => feature.UseTenantsProvider<StoreTenantsProvider>());
    }

    public override void ConfigureHostedServices()
    {
        Module.ConfigureHostedService<SetupMediatorPipelines>();
    }

    /// <inheritdoc />
    public override void Apply()
    {
        Services.Configure(MultitenancyOptions);
        Services.Configure<TenantsOptions>(options => options.IsEnabled = true);

        Services
            .AddScoped<ITenantResolverPipelineInvoker, DefaultTenantResolverPipelineInvoker>()
            .AddScoped<ITenantResolver, DefaultTenantResolver>();
    }
}