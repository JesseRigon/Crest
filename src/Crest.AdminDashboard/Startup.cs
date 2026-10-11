using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Crest.Admin;
using Crest.AdminDashboard.Controllers;
using Crest.AdminDashboard.Drivers;
using Crest.AdminDashboard.Indexes;
using Crest.AdminDashboard.Models;
using Crest.AdminDashboard.Services;
using Crest.ContentManagement;
using Crest.ContentManagement.Display.ContentDisplay;
using Crest.ContentTypes.Events;
using Crest.Data;
using Crest.Data.Migration;
using Crest.Modules;
using Crest.Mvc.Utilities;
using Crest.Security.Permissions;

namespace Crest.AdminDashboard;

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
            areaName: "Crest.AdminDashboard",
            pattern: _adminOptions.AdminUrlPrefix,
            defaults: new { controller = dashboardControllerName, action = nameof(DashboardController.Index) }
        );
    }
}
