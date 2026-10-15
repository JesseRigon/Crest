using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Grove.Admin;
using Grove.AdminDashboard.Controllers;
using Grove.AdminDashboard.Drivers;
using Grove.AdminDashboard.Indexes;
using Grove.AdminDashboard.Models;
using Grove.AdminDashboard.Services;
using Grove.ContentManagement;
using Grove.ContentManagement.Display.ContentDisplay;
using Grove.ContentTypes.Events;
using Grove.Data;
using Grove.Data.Migration;
using Grove.Modules;
using Grove.Mvc.Utilities;
using Grove.Security.Permissions;

namespace Grove.AdminDashboard;

public sealed class Startup : StartupBase
{
    public override int ConfigureOrder => -10;

    private readonly AdminOptions _adminOptions;

    public Startup(IOptions<AdminOptions> adminOptions)
    {
        _adminOptions = adminOptions.Value;
    }

    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddPermissionProvider<Permissions>();

        services.AddScoped<IAdminDashboardService, AdminDashboardService>();
        services.AddIndexProvider<DashboardPartIndexProvider>();

        services.AddContentPart<DashboardPart>()
            .UseDisplayDriver<DashboardPartDisplayDriver>();

        services.AddScoped<IContentDisplayDriver, DashboardContentDisplayDriver>();

        services.AddDataMigration<Migrations>();
        services.AddScoped<IContentDefinitionHandler, DashboardPartContentTypeDefinitionHandler>();
    }

    public override void Configure(IApplicationBuilder builder, IEndpointRouteBuilder routes, IServiceProvider serviceProvider)
    {
        // Dashboard
        var dashboardControllerName = typeof(DashboardController).ControllerName();

        routes.MapAreaControllerRoute(
            name: "AdminDashboard",
            areaName: "Grove.AdminDashboard",
            pattern: _adminOptions.AdminUrlPrefix,
            defaults: new { controller = dashboardControllerName, action = nameof(DashboardController.Index) }
        );
    }
}
