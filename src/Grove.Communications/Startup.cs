using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Grove.Infrastructure.Shells.Admin.Models;
using Grove.Data;
using Grove.Data.Migration;
using Grove.Display.Management.Handlers;
using Grove.Environment.Shell.Configuration;
using Grove.Modules;
using Grove.Display.Navigation;
using Grove.Communications.Handlers;
using Grove.Communications.Indexes;
using Grove.Communications.Migrations;
using Grove.Communications.Models;
using Grove.Communications.Services;
using Grove.Security.Permissions;
using Grove.Access.Users;
using Grove.Access.Users.Models;
using Grove.Communications.Workflows;
using Grove.Workflows;
using Grove.Workflows.Extensions;
using YesSql.Filters.Query;

namespace Grove.Communications;

public sealed class Startup : StartupBase
{
    private readonly IShellConfiguration _shellConfiguration;

    public Startup(IShellConfiguration shellConfiguration)
    {
        _shellConfiguration = shellConfiguration;
    }

    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddScoped<INotificationService, NotificationService>();
        services.AddScoped<INotificationMethodProviderAccessor, NotificationMethodProviderAccessor>();

        services.AddDataMigration<NotificationMigrations>();
        services.AddIndexProvider<NotificationIndexProvider>();
        services.Configure<StoreCollectionOptions>(o => o.Collections.Add(NotificationConstants.NotificationCollection));
        services.AddScoped<INotificationEvents, CoreNotificationEventsHandler>();

        services.AddPermissionProvider<NotificationPermissionsProvider>();
        services.AddTransient<INotificationAdminListFilterProvider, DefaultNotificationsAdminListFilterProvider>();
        services.AddSingleton<INotificationAdminListFilterParser>(sp =>
        {
            var filterProviders = sp.GetServices<INotificationAdminListFilterProvider>();
            var builder = new QueryEngineBuilder<Notification>();
            foreach (var provider in filterProviders)
            {
                provider.Build(builder);
            }

            var parser = builder.Build();

            return new DefaultNotificationAdminListFilterParser(parser);
        });

        services.Configure<NotificationOptions>(_shellConfiguration.GetSection("Grove_Notifications"));

        services.AddScoped<INotificationEvents, CacheNotificationEventsHandler>();
        services.AddApiContributor<Api.NotificationPreferencesApi>();
    }
}

[RequireFeatures(WorkflowsConstants.FeatureId, UserConstants.Features.Users)]
public sealed class WorkflowsStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddScoped<IWorkflowActivityProvider, NotificationsWorkflowProvider>();
        services.ConfigureGroveWorkflows(workflows => workflows.AddActivitiesFrom<WorkflowsStartup>());
    }
}

[Feature("Grove.Communications.Notifications.Email")]
public sealed class EmailNotificationsStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddScoped<INotificationMethodProvider, EmailNotificationProvider>();
    }
}
