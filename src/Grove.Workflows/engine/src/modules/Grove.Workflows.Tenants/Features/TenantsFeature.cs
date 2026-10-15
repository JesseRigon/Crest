using Grove.Workflows.Common.Features;
using Grove.Workflows.Common.Multitenancy;
using Grove.Workflows.Features.Abstractions;
using Grove.Workflows.Features.Attributes;
using Grove.Workflows.Features.Services;
using Grove.Workflows.Tenants.Mediator.Tasks;
using Grove.Workflows.Tenants.Options;
using Grove.Workflows.Tenants.Providers;
using Microsoft.Extensions.DependencyInjection;

namespace Grove.Workflows.Tenants.Features;

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