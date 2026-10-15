using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Grove.Infrastructure.Shells.Admin.Models;
using Grove.Display.Management;
using Grove.Display.Management.Handlers;
using Grove.Display.Management.Theming;
using Grove.Environment.Shell.Configuration;
using Grove.Environment.Shell.Scope;
using Grove.Modules;
using Grove.Display.Navigation;
using Grove.Security.Permissions;
using Grove.Infrastructure.Settings.Deployment;

namespace Grove.Infrastructure.Shells.Admin;

public sealed class Startup : StartupBase
{
    private readonly IShellConfiguration _configuration;

    public Startup(IShellConfiguration configuration)
    {
        _configuration = configuration;
    }

    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddScoped<TitleBarSettingsStore>();
        services.AddApiContributor<Api.AdminSettingsApi>();
        services.AddApiContributor<Api.TitleBarSettingsApi>();
        services.AddApiContributor<Api.ThemeSettingsApi>();
        services.AddNavigation();

        services.AddPermissionProvider<Permissions>();
        services.AddScoped<IThemeSelector, AdminThemeSelector>();
        services.AddScoped<IAdminThemeService, AdminThemeService>();
        services.AddPermissionProvider<PermissionsAdminSettings>();

        services.Configure<AdminOptions>(_configuration.GetSection("Grove_Admin"));
    }

}

[RequireFeatures("Grove.Deployment")]
public sealed class DeploymentStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddSiteSettingsPropertyDeploymentStep<AdminSettings>();
    }
}
