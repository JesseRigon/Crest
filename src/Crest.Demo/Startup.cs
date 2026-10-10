using Fluid;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Mvc.ApplicationModels;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Crest.Admin;
using Crest.BackgroundTasks;
using Crest.ContentManagement;
using Crest.ContentManagement.Display.ContentDisplay;
using Crest.Data.Migration;
using Crest.Demo.Commands;
using Crest.Demo.ContentElementDisplays;
using Crest.Demo.Controllers;
using Crest.Demo.Drivers;
using Crest.Demo.Models;
using Crest.Demo.Services;
using Crest.Demo.TagHelpers;
using Crest.DisplayManagement;
using Crest.DisplayManagement.Descriptors;
using Crest.DisplayManagement.Handlers;
using Crest.Environment.Commands;
using Crest.Modules;
using Crest.Mvc.Core.Utilities;
using Crest.Navigation;
using Crest.Security.Permissions;
using Crest.Users.Models;
using Crest.Users.Services;

namespace Crest.Demo;

public sealed class Startup : StartupBase
{
    private readonly AdminOptions _adminOptions;

    public Startup(IOptions<AdminOptions> adminOptions)
    {
        _adminOptions = adminOptions.Value;
    }

    public override void Configure(IApplicationBuilder builder, IEndpointRouteBuilder routes, IServiceProvider serviceProvider)
    {
        routes.MapAreaControllerRoute(
            name: "Demo.Home.Index",
            areaName: "Crest.Demo",
            pattern: "Home/Index",
            defaults: new { controller = "Home", action = "Index" }
        );

        routes.MapAreaControllerRoute(
            name: "Demo.Home.Display",
            areaName: "Crest.Demo",
            pattern: "Home/Display/{contentItemId}",
            defaults: new { controller = "Home", action = "Display" }
        );

        routes.MapAreaControllerRoute(
            name: "Demo.Home.Error",
            areaName: "Crest.Demo",
            pattern: "Home/IndexError",
            defaults: new { controller = "Home", action = "IndexError" }
        );

        var demoAdminControllerName = typeof(AdminController).ControllerName();

        // While you can define admin routes like this, we suggest adding the [Admin("path after the admin prefix")]
        // attribute to the action's method instead. That way the route is visible right next to the action which
        // makes the code easier to understand. You can find an example in this module at ContentController.Edit.
        routes.MapAreaControllerRoute(
            name: "Demo.Admin",
            areaName: "Crest.Demo",
            pattern: _adminOptions.AdminUrlPrefix + "/Demo/Admin",
            defaults: new { controller = demoAdminControllerName, action = nameof(AdminController.Index) }
        );

        var demoContentControllerName = typeof(ContentController).ControllerName();

        builder.UseMiddleware<NonBlockingMiddleware>();
        builder.UseMiddleware<BlockingMiddleware>();
    }

    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddScoped<ITestDependency, ClassFoo>();
        services.AddScoped<ICommandHandler, DemoCommands>();
        services.AddSingleton<IBackgroundTask, TestBackgroundTask>();
        services.AddShapeTableProvider<DemoShapeProvider>();
        services.AddShapeAttributes<DemoShapeProvider>();
        services.AddNavigationProvider<AdminMenu>();
        services.AddScoped<IContentDisplayDriver, TestContentElementDisplayDriver>();
        services.AddDataMigration<Migrations>();
        services.AddPermissionProvider<Permissions>();
        services.AddContentPart<TestContentPartA>();
        services.AddScoped<IUserClaimsProvider, UserProfileClaimsProvider>();

        services.AddDisplayDriver<User, UserProfileDisplayDriver>();

        services.Configure<RazorPagesOptions>(options =>
        {
            // Add a custom page folder route (only applied to non admin pages)
            options.Conventions.AddAreaFolderRoute("Crest.Demo", "/", "Demo");

            // Add a custom admin page folder route (only applied to admin pages) using the current admin prefix
            options.Conventions.AddAdminAreaFolderRoute("Crest.Demo", "/Admin", _adminOptions.AdminUrlPrefix + "/Demo");

            // Add a custom admin page folder route without using the current admin prefix
            options.Conventions.AddAdminAreaFolderRoute("Crest.Demo", "/Foo/Admin", "Manage/Foo");

            // Add a custom admin page route using the current admin prefix
            options.Conventions.AddAreaPageRoute("Crest.Demo", "/OutsideAdmin", _adminOptions.AdminUrlPrefix + "/Outside");

            // Add a custom page route
            options.Conventions.AddAreaPageRoute("Crest.Demo", "/Hello", "Hello");

            // This declaration would define an home page
            // options.Conventions.AddAreaPageRoute("Crest.Demo", "/Hello", "");
        });

        services.AddTagHelpers(typeof(BazTagHelper).Assembly);

        services.Configure<TemplateOptions>(o =>
        {
            o.MemberAccessStrategy.Register<Crest.Demo.ViewModels.TodoViewModel>();
        });
    }
}
