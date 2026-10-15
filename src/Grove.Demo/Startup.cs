using Fluid;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Mvc.ApplicationModels;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Grove.Admin;
using Grove.BackgroundTasks;
using Grove.ContentManagement;
using Grove.ContentManagement.Display.ContentDisplay;
using Grove.Data.Migration;
using Grove.Demo.Commands;
using Grove.Demo.ContentElementDisplays;
using Grove.Demo.Controllers;
using Grove.Demo.Drivers;
using Grove.Demo.Models;
using Grove.Demo.Services;
using Grove.Demo.TagHelpers;
using Grove.DisplayManagement;
using Grove.DisplayManagement.Descriptors;
using Grove.DisplayManagement.Handlers;
using Grove.Environment.Commands;
using Grove.Modules;
using Grove.Mvc.Utilities;
using Grove.Navigation;
using Grove.Security.Permissions;
using Grove.Users.Models;
using Grove.Users.Services;

namespace Grove.Demo;

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
            areaName: "Grove.Demo",
            pattern: "Home/Index",
            defaults: new { controller = "Home", action = "Index" }
        );

        routes.MapAreaControllerRoute(
            name: "Demo.Home.Display",
            areaName: "Grove.Demo",
            pattern: "Home/Display/{contentItemId}",
            defaults: new { controller = "Home", action = "Display" }
        );

        routes.MapAreaControllerRoute(
            name: "Demo.Home.Error",
            areaName: "Grove.Demo",
            pattern: "Home/IndexError",
            defaults: new { controller = "Home", action = "IndexError" }
        );

        var demoAdminControllerName = typeof(AdminController).ControllerName();

        // While you can define admin routes like this, we suggest adding the [Admin("path after the admin prefix")]
        // attribute to the action's method instead. That way the route is visible right next to the action which
        // makes the code easier to understand. You can find an example in this module at ContentController.Edit.
        routes.MapAreaControllerRoute(
            name: "Demo.Admin",
            areaName: "Grove.Demo",
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
        services.AddPageSource<AdminPages>();
        services.AddScoped<IContentDisplayDriver, TestContentElementDisplayDriver>();
        services.AddDataMigration<Migrations>();
        services.AddPermissionProvider<Permissions>();
        services.AddContentPart<TestContentPartA>();
        services.AddScoped<IUserClaimsProvider, UserProfileClaimsProvider>();

        services.AddDisplayDriver<User, UserProfileDisplayDriver>();

        services.Configure<RazorPagesOptions>(options =>
        {
            // Add a custom page folder route (only applied to non admin pages)
            options.Conventions.AddAreaFolderRoute("Grove.Demo", "/", "Demo");

            // Add a custom admin page folder route (only applied to admin pages) using the current admin prefix
            options.Conventions.AddAdminAreaFolderRoute("Grove.Demo", "/Admin", _adminOptions.AdminUrlPrefix + "/Demo");

            // Add a custom admin page folder route without using the current admin prefix
            options.Conventions.AddAdminAreaFolderRoute("Grove.Demo", "/Foo/Admin", "Manage/Foo");

            // Add a custom admin page route using the current admin prefix
            options.Conventions.AddAreaPageRoute("Grove.Demo", "/OutsideAdmin", _adminOptions.AdminUrlPrefix + "/Outside");

            // Add a custom page route
            options.Conventions.AddAreaPageRoute("Grove.Demo", "/Hello", "Hello");

            // This declaration would define an home page
            // options.Conventions.AddAreaPageRoute("Grove.Demo", "/Hello", "");
        });

        services.AddTagHelpers(typeof(BazTagHelper).Assembly);

        services.Configure<TemplateOptions>(o =>
        {
            o.MemberAccessStrategy.Register<Grove.Demo.ViewModels.TodoViewModel>();
        });
    }
}
