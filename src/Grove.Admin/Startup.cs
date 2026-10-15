using Fluid;
using Fluid.Values;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ApplicationModels;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Grove.Admin.Controllers;
using Grove.Admin.Drivers;
using Grove.Admin.Models;
using Grove.DisplayManagement;
using Grove.DisplayManagement.Handlers;
using Grove.DisplayManagement.ModelBinding;
using Grove.DisplayManagement.Theming;
using Grove.Environment.Shell.Configuration;
using Grove.Environment.Shell.Scope;
using Grove.Liquid;
using Grove.Modules;
using Grove.Mvc.Utilities;
using Grove.Mvc.Routing;
using Grove.Navigation;
using Grove.Security.Permissions;
using Grove.Settings.Deployment;

namespace Grove.Admin;

public sealed class Startup : StartupBase
{
    private readonly AdminOptions _adminOptions;
    private readonly IShellConfiguration _configuration;

    public Startup(IOptions<AdminOptions> adminOptions, IShellConfiguration configuration)
    {
        _adminOptions = adminOptions.Value;
        _configuration = configuration;
    }

    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddScoped<TitleBarSettingsStore>();
        services.AddApiContributor<Api.AdminSettingsApi>();
        services.AddApiContributor<Api.TitleBarSettingsApi>();
        services.AddApiContributor<Api.ThemeSettingsApi>();
        services.AddNavigation();

        services.Configure<MvcOptions>((options) =>
        {
            options.Filters.Add<AdminFilter>();

            // Ordered to be called before any global filter.
            options.Filters.Add<AdminZoneFilter>(-1000);
        });

        services.AddTransient<IAreaControllerRouteMapper, AdminAreaControllerRouteMapper>();
        services.AddPermissionProvider<Permissions>();
        services.AddScoped<IThemeSelector, AdminThemeSelector>();
        services.AddScoped<IAdminThemeService, AdminThemeService>();
        services.AddSiteDisplayDriver<AdminSiteSettingsDisplayDriver>();
        services.AddPermissionProvider<PermissionsAdminSettings>();
        services.AddSingleton<IPageRouteModelProvider, AdminPageRouteModelProvider>();
        services.AddDisplayDriver<Navbar, VisitSiteNavbarDisplayDriver>();
        services.AddShapeTableProvider<AdminDashboardShapeTableProvider>();

        services.Configure<AdminOptions>(_configuration.GetSection("Grove_Admin"));
    }

    public override void Configure(IApplicationBuilder builder, IEndpointRouteBuilder routes, IServiceProvider serviceProvider)
    {
        routes.MapAreaControllerRoute(
            name: "Admin",
            areaName: "Grove.Admin",
            pattern: _adminOptions.AdminUrlPrefix,
            defaults: new { controller = typeof(AdminController).ControllerName(), action = nameof(AdminController.Index) }
        );
    }
}

public sealed class AdminPagesStartup : StartupBase
{
    public override int Order
        => PlatformConstants.ConfigureOrder.AdminPages;

    public override void ConfigureServices(IServiceCollection services)
    {
        services.Configure<RazorPagesOptions>((options) =>
        {
            var adminOptions = ShellScope.Services.GetRequiredService<IOptions<AdminOptions>>().Value;
            options.Conventions.Add(new AdminPageRouteModelConvention(adminOptions.AdminUrlPrefix));
        });
    }
}

[RequireFeatures("Grove.Deployment")]
public sealed class DeploymentStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddSiteSettingsPropertyDeploymentStep<AdminSettings, DeploymentStartup>(S => S["Admin settings"], S => S["Exports the admin settings."]);
    }
}

[RequireFeatures("Grove.Liquid")]
public sealed class LiquidStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.Configure<TemplateOptions>(o =>
        {
            o.Scope.SetValue(nameof(Navbar), new FunctionValue(async (args, ctx) =>
            {
                if (ctx is LiquidTemplateContext context)
                {
                    var displayManager = context.Services.GetRequiredService<IDisplayManager<Navbar>>();
                    var updateModelAccessor = context.Services.GetRequiredService<IUpdateModelAccessor>();

                    var shape = await displayManager.BuildDisplayAsync(updateModelAccessor.ModelUpdater);

                    return FluidValue.Create(shape, ctx.Options);
                }

                return NilValue.Instance;
            }));
        });
    }
}
