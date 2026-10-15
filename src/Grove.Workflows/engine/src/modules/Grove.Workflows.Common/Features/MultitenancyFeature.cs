using Grove.Workflows.Common.Multitenancy;
using Grove.Workflows.Common.Multitenancy.EventHandlers;
using Grove.Workflows.Common.Multitenancy.HostedServices;
using Grove.Workflows.Common.RecurringTasks;
using Grove.Workflows.Features.Abstractions;
using Grove.Workflows.Features.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Grove.Workflows.Common.Features;

public class MultitenancyFeature(IModule module) : FeatureBase(module)
{
    private Func<IServiceProvider, ITenantsProvider> _tenantsProviderFactory = sp => sp.GetRequiredService<DefaultTenantsProvider>();
    
    public MultitenancyFeature UseTenantsProvider<TTenantsProvider>() where TTenantsProvider : class, ITenantsProvider
    {
        Services.TryAddScoped<TTenantsProvider>();
        return UseTenantsProvider(sp => sp.GetRequiredService<TTenantsProvider>());
    }
    
    public MultitenancyFeature UseTenantsProvider(Func<IServiceProvider, ITenantsProvider> tenantsProviderFactory)
    {
        _tenantsProviderFactory = tenantsProviderFactory;
        return this;
    }

    public override void ConfigureHostedServices()
    {
        Module
            .ConfigureHostedService<ActivateTenants>(-1)
            ;
    }

    public override void Apply()
    {
        Services
            .AddSingleton<ITenantScopeFactory, DefaultTenantScopeFactory>()
            .AddSingleton<ITenantAccessor, DefaultTenantAccessor>()
            .AddSingleton<ITenantFinder, DefaultTenantFinder>()
            .AddSingleton<ITenantService, DefaultTenantService>()

            // TenantTaskManager handles all task lifecycle in the correct order
            .AddSingleton<TenantTaskManager>()
            .AddSingleton<ITenantActivatedEvent>(sp => sp.GetRequiredService<TenantTaskManager>())
            .AddSingleton<ITenantDeactivatedEvent>(sp => sp.GetRequiredService<TenantTaskManager>())

            .AddSingleton<RecurringTaskScheduleManager>()
            .AddSingleton<TenantEventsManager>()
            .AddScoped<DefaultTenantsProvider>()
            .AddScoped<DefaultTenantResolver>()
            .AddScoped<ITaskExecutor, TaskExecutor>()
            .AddScoped<IBackgroundTaskStarter, TaskExecutor>()
            .AddScoped(_tenantsProviderFactory);
    }
}