using Microsoft.Extensions.DependencyInjection;
using Crest.Deployment;
using Crest.DisplayManagement;
using Crest.DisplayManagement.Handlers;
using Crest.DisplayManagement.Theming;
using Crest.Modules;
using Crest.Navigation;
using Crest.Recipes;
using Crest.Security.Permissions;
using Crest.Themes.Deployment;
using Crest.Themes.Drivers;
using Crest.Themes.Models;
using Crest.Themes.Recipes;
using Crest.Themes.Services;

namespace Crest.Themes;

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
        services.AddDeployment<ThemesDeploymentSource, ThemesDeploymentStep, ThemesDeploymentStepDriver>();
        services.AddDisplayDriver<ThemeEntry, ThemeEntryDisplayDriver>();
        services.AddShapeTableProvider<AdminDashboardShapeTableProvider>();
    }
}
