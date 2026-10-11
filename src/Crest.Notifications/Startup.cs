using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Crest.Admin.Models;
using Crest.Data;
using Crest.Data.Migration;
using Crest.DisplayManagement.Handlers;
using Crest.Environment.Shell.Configuration;
using Crest.Modules;
using Crest.Navigation;
using Crest.Notifications.Drivers;
using Crest.Notifications.Endpoints.Management;
using Crest.Notifications.Handlers;
using Crest.Notifications.Indexes;
using Crest.Notifications.Migrations;
using Crest.Notifications.Models;
using Crest.Notifications.Services;
using Crest.Security.Permissions;
using Crest.Users;
using Crest.Users.Models;
using Crest.Notifications.Workflows;
using Crest.Workflows;
using Crest.Workflows.Extensions;
using YesSql.Filters.Query;

namespace Crest.Notifications;

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
        services.AddScoped<INotificationsAdminListQueryService, DefaultNotificationsAdminListQueryService>();
        services.Configure<StoreCollectionOptions>(o => o.Collections.Add(NotificationConstants.NotificationCollection));
        services.AddScoped<INotificationEvents, CoreNotificationEventsHandler>();

        services.AddPermissionProvider<NotificationPermissionsProvider>();
        services.AddDisplayDriver<ListNotificationOptions, ListNotificationOptionsDisplayDriver>();
        services.AddDisplayDriver<Notification, NotificationDisplayDriver>();
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

        services.Configure<NotificationOptions>(_shellConfiguration.GetSection("Crest_Notifications"));

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
        services.ConfigureCrestWorkflows(workflows => workflows.AddActivitiesFrom<WorkflowsStartup>());
    }
}

[Feature("Crest.Notifications.Email")]
public sealed class EmailNotificationsStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddScoped<INotificationMethodProvider, EmailNotificationProvider>();
    }
}
