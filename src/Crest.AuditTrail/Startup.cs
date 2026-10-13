using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using Crest.AuditTrail.Drivers;
using Crest.AuditTrail.Indexes;
using Crest.AuditTrail.Models;
using Crest.AuditTrail.Navigation;
using Crest.AuditTrail.Services;
using Crest.AuditTrail.Services.Models;
using Crest.AuditTrail.Settings;
using Crest.AuditTrail.ViewModels;
using Crest.BackgroundTasks;
using Crest.Data;
using Crest.Data.Migration;
using Crest.DisplayManagement.Handlers;
using Crest.Modules;
using Crest.Navigation;
using Crest.Security.Permissions;
using Crest.Settings.Deployment;
using YesSql.Filters.Query;

namespace Crest.AuditTrail;

public sealed class Startup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        // Add ILookupNormalizer as Singleton because it is needed by Users
        services.TryAddSingleton<ILookupNormalizer, UpperInvariantLookupNormalizer>();

        services.AddScoped<IAuditTrailManager, AuditTrailManager>();

        services.AddDisplayDriver<AuditTrailEvent, AuditTrailEventDisplayDriver>();

        services.AddSingleton<IAuditTrailIdGenerator, AuditTrailIdGenerator>();

        services.Configure<StoreCollectionOptions>(o => o.Collections.Add(AuditTrailEvent.Collection));

        services.AddDataMigration<Migrations>();
        services.AddIndexProvider<AuditTrailEventIndexProvider>();
        services.AddIndexProvider<AuditTrailAttributionIndexProvider>();
        services.AddSingleton<IBackgroundTask, AuditTrailBackgroundTask>();

        services.AddPermissionProvider<Permissions>();
        services.AddNavigationProvider<AuditTrailAdminMenu>();

        services.AddSiteDisplayDriver<AuditTrailSettingsDisplayDriver>();
        services.AddSiteDisplayDriver<AuditTrailTrimmingSettingsDisplayDriver>();

        services.AddDisplayDriver<AuditTrailIndexOptions, AuditTrailOptionsDisplayDriver>();

        services.AddScoped<IAuditTrailAdminListQueryService, DefaultAuditTrailAdminListQueryService>();

        services.AddSingleton<IAuditTrailAdminListFilterParser>(sp =>
        {
            var filterProviders = sp.GetServices<IAuditTrailAdminListFilterProvider>();
            var builder = new QueryEngineBuilder<AuditTrailEvent>();
            foreach (var provider in filterProviders)
            {
                provider.Build(builder);
            }

            var parser = builder.Build();

            return new DefaultAuditTrailAdminListFilterParser(parser);
        });

        services.AddTransient<IAuditTrailAdminListFilterProvider, DefaultAuditTrailAdminListFilterProvider>();

        services.AddOptions<AuditTrailOptions>();
        services.AddTransient<IConfigureOptions<AuditTrailOptions>, ImpersonationAuditTrailEventConfiguration>();

        // Tenant-machinery writes (docs/audit.md › step 3): roles, features, settings, themes.
        services.AddTransient<IConfigureOptions<AuditTrailOptions>, Recorders.TenantAuditTrailEventConfiguration>();
        services.AddScoped<Recorders.TenantAuditTrailRecorder>();
        services.AddScoped<Recorders.RoleAuditTrailEventHandler>();
        services.AddScoped<Crest.Security.IRoleCreatedEventHandler>(sp => sp.GetRequiredService<Recorders.RoleAuditTrailEventHandler>());
        services.AddScoped<Crest.Security.IRoleUpdatedEventHandler>(sp => sp.GetRequiredService<Recorders.RoleAuditTrailEventHandler>());
        services.AddScoped<Crest.Security.IRoleRemovedEventHandler>(sp => sp.GetRequiredService<Recorders.RoleAuditTrailEventHandler>());
        services.AddScoped<Crest.Environment.Shell.IFeatureEventHandler, Recorders.FeatureAuditTrailEventHandler>();
        DecorateSiteService(services);

        // The Crest audit pages and their API (docs/audit.md › step 4).
        services.AddScoped<Api.AuditTrailFeedService>();
        services.AddScoped<Crest.Access.IRoutePermissionProvider, Api.AuditTrailRoutePermissionProvider>();

        services.Configure<AuditTrailAdminListOptions>(options =>
        {
            options
                .ForSort("time-desc", b => b
                    .WithQuery((val, query) => query.With<AuditTrailEventIndex>().OrderByDescending(i => i.CreatedUtc))
                    .WithSelectListItem<Startup>((S, opt, model) => new SelectListItem(S["Newest"], opt.Value, model.Sort == string.Empty))
                    .AsDefault())

                .ForSort("time-asc", b => b
                    .WithQuery((val, query) => query.With<AuditTrailEventIndex>().OrderBy(i => i.CreatedUtc))
                    .WithSelectListItem<Startup>((S, opt, model) => new SelectListItem(S["Oldest"], opt.Value, model.Sort == opt.Value)))

                .ForSort("category-asc-time-desc", b => b
                    .WithQuery((val, query) => query.With<AuditTrailEventIndex>().OrderBy(i => i.Category).ThenByDescending(i => i.CreatedUtc))
                    .WithSelectListItem<Startup>((S, opt, model) => new SelectListItem(S["Category"], opt.Value, model.Sort == opt.Value)))

                .ForSort("category-asc-time-asc", b => b
                    .WithQuery((val, query) => query.With<AuditTrailEventIndex>().OrderBy(i => i.Category).ThenBy(i => i.CreatedUtc)))

                .ForSort("category-desc-time-desc", b => b
                    .WithQuery((val, query) => query.With<AuditTrailEventIndex>().OrderByDescending(i => i.Category).ThenByDescending(i => i.CreatedUtc)))

                .ForSort("category-desc-time-asc", b => b
                    .WithQuery((val, query) => query.With<AuditTrailEventIndex>().OrderByDescending(i => i.Category).ThenBy(i => i.CreatedUtc)))

                .ForSort("event-asc-time-desc", b => b
                    .WithQuery((val, query) => query.With<AuditTrailEventIndex>().OrderBy(i => i.Name).ThenByDescending(i => i.CreatedUtc))
                    .WithSelectListItem<Startup>((S, opt, model) => new SelectListItem(S["Event"], opt.Value, model.Sort == opt.Value)))

                .ForSort("event-asc-time-asc", b => b
                    .WithQuery((val, query) => query.With<AuditTrailEventIndex>().OrderBy(i => i.Name).ThenBy(i => i.CreatedUtc)))

                .ForSort("event-desc-time-desc", b => b
                    .WithQuery((val, query) => query.With<AuditTrailEventIndex>().OrderByDescending(i => i.Name).ThenByDescending(i => i.CreatedUtc)))

                .ForSort("event-desc-time-asc", b => b
                    .WithQuery((val, query) => query.With<AuditTrailEventIndex>().OrderByDescending(i => i.Name).ThenBy(i => i.CreatedUtc)))

                .ForSort("user-asc-time-asc", b => b
                    .WithQuery((val, query) => query.With<AuditTrailEventIndex>().OrderBy(i => i.NormalizedUserName).ThenBy(i => i.CreatedUtc))
                    .WithSelectListItem<Startup>((S, opt, model) => new SelectListItem(S["User"], opt.Value, model.Sort == opt.Value)))

                .ForSort("user-desc-time-desc", b => b
                    .WithQuery((val, query) => query.With<AuditTrailEventIndex>().OrderByDescending(i => i.NormalizedUserName).ThenByDescending(i => i.CreatedUtc)))

                .ForSort("user-desc-time-asc", b => b
                    .WithQuery((val, query) => query.With<AuditTrailEventIndex>().OrderByDescending(i => i.NormalizedUserName).ThenBy(i => i.CreatedUtc)));
        });
    }

    private static void DecorateSiteService(IServiceCollection services)
    {
        var descriptor = services.LastOrDefault(d => d.ServiceType == typeof(Crest.Settings.ISiteService));
        if (descriptor is null)
        {
            return;
        }

        services.Remove(descriptor);
        services.Add(new ServiceDescriptor(typeof(Crest.Settings.ISiteService), sp =>
        {
            var inner = descriptor.ImplementationInstance as Crest.Settings.ISiteService
                ?? (descriptor.ImplementationFactory is not null
                    ? (Crest.Settings.ISiteService)descriptor.ImplementationFactory(sp)
                    : (Crest.Settings.ISiteService)ActivatorUtilities.CreateInstance(sp, descriptor.ImplementationType!));
            return new Recorders.AuditingSiteService(inner);
        }, descriptor.Lifetime));
    }
}

[RequireFeatures("Crest.Deployment")]
public sealed class DeploymentStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddSiteSettingsPropertyDeploymentStep<AuditTrailSettings, DeploymentStartup>(S => S["Audit Trail settings"], S => S["Exports the audit trail settings."]);
        services.AddSiteSettingsPropertyDeploymentStep<AuditTrailTrimmingSettings, DeploymentStartup>(S => S["Audit Trail Trimming settings"], S => S["Exports the audit trail trimming settings."]);
    }
}
