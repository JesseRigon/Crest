using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using Grove.Api;
using Grove.AuditTrail.Indexes;
using Grove.AuditTrail.Models;
using Grove.AuditTrail.Navigation;
using Grove.AuditTrail.Services;
using Grove.AuditTrail.Services.Models;
using Grove.AuditTrail.Settings;
using Grove.BackgroundTasks;
using Grove.Data;
using Grove.Data.Migration;
using Grove.Display.Management.Handlers;
using Grove.Modules;
using Grove.Display.Navigation;
using Grove.Security.Permissions;
using Grove.Infrastructure.Settings.Deployment;

namespace Grove.AuditTrail;

public sealed class Startup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        // Add ILookupNormalizer as Singleton because it is needed by Users
        services.TryAddSingleton<ILookupNormalizer, UpperInvariantLookupNormalizer>();

        services.AddScoped<IAuditTrailManager, AuditTrailManager>();

        services.AddSingleton<IAuditTrailIdGenerator, AuditTrailIdGenerator>();

        services.Configure<StoreCollectionOptions>(o => o.Collections.Add(AuditTrailEvent.Collection));

        services.AddDataMigration<Migrations>();
        services.AddIndexProvider<AuditTrailEventIndexProvider>();
        services.AddIndexProvider<AuditTrailAttributionIndexProvider>();
        services.AddSingleton<IBackgroundTask, AuditTrailBackgroundTask>();

        services.AddPermissionProvider<Permissions>();

        services.AddOptions<AuditTrailOptions>();
        services.AddTransient<IConfigureOptions<AuditTrailOptions>, ImpersonationAuditTrailEventConfiguration>();

        // Tenant-machinery writes (docs/audit.md › step 3): roles, features, settings, themes.
        services.AddTransient<IConfigureOptions<AuditTrailOptions>, Recorders.TenantAuditTrailEventConfiguration>();
        services.AddScoped<Recorders.TenantAuditTrailRecorder>();
        services.AddScoped<Recorders.RoleAuditTrailEventHandler>();
        services.AddScoped<Grove.Security.IRoleCreatedEventHandler>(sp => sp.GetRequiredService<Recorders.RoleAuditTrailEventHandler>());
        services.AddScoped<Grove.Security.IRoleUpdatedEventHandler>(sp => sp.GetRequiredService<Recorders.RoleAuditTrailEventHandler>());
        services.AddScoped<Grove.Security.IRoleRemovedEventHandler>(sp => sp.GetRequiredService<Recorders.RoleAuditTrailEventHandler>());
        services.AddScoped<Grove.Environment.Shell.IFeatureEventHandler, Recorders.FeatureAuditTrailEventHandler>();
        DecorateSiteService(services);

        // The Grove audit pages and their API (docs/audit.md › step 4).
        services.AddScoped<Api.AuditTrailFeedService>();
        services.AddApiContributor<Api.AuditTrailApi>();
        services.AddPageSource<Navigation.AuditTrailPages>();
    }

    private static void DecorateSiteService(IServiceCollection services)
    {
        var descriptor = services.LastOrDefault(d => d.ServiceType == typeof(Grove.Infrastructure.Settings.ISiteService));
        if (descriptor is null)
        {
            return;
        }

        services.Remove(descriptor);
        services.Add(new ServiceDescriptor(typeof(Grove.Infrastructure.Settings.ISiteService), sp =>
        {
            var inner = descriptor.ImplementationInstance as Grove.Infrastructure.Settings.ISiteService
                ?? (descriptor.ImplementationFactory is not null
                    ? (Grove.Infrastructure.Settings.ISiteService)descriptor.ImplementationFactory(sp)
                    : (Grove.Infrastructure.Settings.ISiteService)ActivatorUtilities.CreateInstance(sp, descriptor.ImplementationType!));
            return new Recorders.AuditingSiteService(inner);
        }, descriptor.Lifetime));
    }
}

[RequireFeatures("Grove.Deployment")]
public sealed class DeploymentStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddSiteSettingsPropertyDeploymentStep<AuditTrailSettings>();
        services.AddSiteSettingsPropertyDeploymentStep<AuditTrailTrimmingSettings>();
    }
}
