using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Grove.Admin.Models;
using Grove.Data;
using Grove.Data.Migration;
using Grove.DisplayManagement.Handlers;
using Grove.Environment.Shell.Configuration;
using Grove.Modules;
using Grove.Navigation;
using Grove.Notifications.Drivers;
using Grove.Notifications.Endpoints.Management;
using Grove.Notifications.Handlers;
using Grove.Notifications.Indexes;
using Grove.Notifications.Migrations;
using Grove.Notifications.Models;
using Grove.Notifications.Services;
using Grove.Security.Permissions;
using Grove.Users;
using Grove.Users.Models;
using Grove.Notifications.Workflows;
using Grove.Workflows;
using Grove.Workflows.Extensions;
using YesSql.Filters.Query;

namespace Grove.Notifications;

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

        services.AddResourceConfiguration<NotificationOptionsConfiguration>();
        services.AddDisplayDriver<User, UserNotificationPreferencesPartDisplayDriver>();
        services.AddDisplayDriver<Navbar, NotificationNavbarDisplayDriver>();
        services.AddScoped<INotificationEvents, CacheNotificationEventsHandler>();
    }

    public override void Configure(IApplicationBuilder app, IEndpointRouteBuilder routes, IServiceProvider serviceProvider)
    {
        routes.AddMarkAsReadEndpoint();
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

[Feature("Grove.Notifications.Email")]
public sealed class EmailNotificationsStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddScoped<INotificationMethodProvider, EmailNotificationProvider>();
    }
}
