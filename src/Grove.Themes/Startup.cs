using Microsoft.Extensions.DependencyInjection;
using Grove.Deployment;
using Grove.Display.Management;
using Grove.Display.Management.Handlers;
using Grove.Display.Management.Theming;
using Grove.Modules;
using Grove.Display.Navigation;
using Grove.Infrastructure.Recipes;
using Grove.Security.Permissions;
using Grove.Themes.Deployment;
using Grove.Themes.Recipes;
using Grove.Themes.Services;

namespace Grove.Themes;

/// <summary>
/// These services are registered on the tenant service collection.
/// </summary>
public sealed class Startup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddResourceConfiguration<ResourceManagementOptionsConfiguration>();
        services.AddRecipeExecutionStep<ThemesStep>();
        services.AddPermissionProvider<Permissions>();
        services.AddScoped<IThemeSelector, SiteThemeSelector>();
        services.AddScoped<ISiteThemeService, SiteThemeService>();
        services.AddScoped<IThemeService, ThemeService>();
        services.AddScoped<ThemeTogglerService>();
        // The shell contracts (docs/shells-and-themes.md): the member theme and shell compatibility.
        services.AddScoped<IMemberThemeService, MemberThemeService>();
        services.AddScoped<IShellCompatibilityService, ShellCompatibilityService>();
        services.AddApiContributor<Api.ThemesApi>();
        services.AddDeployment<ThemesDeploymentSource, ThemesDeploymentStep>();
    }
}
