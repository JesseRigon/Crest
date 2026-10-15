using System.Net.Mime;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.Net.Http.Headers;
using Grove.Deployment;
using Grove.Display.Management;
using Grove.Environment.Shell;
using Grove.Environment.Shell.Configuration;
using Grove.Environment.Shell.Distributed;
using Grove.Modules;
using Grove.Modules.FileProviders;
using Grove.Display.Navigation;
using Grove.Infrastructure.Recipes;
using Grove.Security.Permissions;
using Grove.Infrastructure.Setup;
using Grove.Infrastructure.Tenants.Deployment;
using Grove.Infrastructure.Tenants.Recipes;
using Grove.Infrastructure.Tenants.Services;

namespace Grove.Infrastructure.Tenants;

public sealed class Startup : StartupBase
{
    private readonly IShellConfiguration _shellConfiguration;

    public Startup(IShellConfiguration shellConfiguration)
    {
        _shellConfiguration = shellConfiguration;
    }

    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddApiContributor<Api.TenantsApi>();
        services.AddApiContributor<Api.TenantApi>();
        services.AddPermissionProvider<Permissions>();
        services.AddScoped<TenantDatabasePatternResolver>();
        services.AddScoped<ITenantValidator, TenantValidator>();
        // Create, edit and setup for the native Tenants page and the tenants API alike.
        services.AddScoped<TenantAdministration>();
        services.AddSetup();

        services.Configure<TenantsOptions>(_shellConfiguration.GetSection("Grove_Tenants"));
    }
}

[Feature("Grove.Infrastructure.Tenants.FileProvider")]
public sealed class FileProviderStartup : StartupBase
{
    /// <summary>
    /// The path in the tenant's App_Data folder containing the files.
    /// </summary>
    private const string AssetsPath = "wwwroot";

    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddSingleton<ITenantFileProvider>(serviceProvider =>
        {
            var shellOptions = serviceProvider.GetRequiredService<IOptions<ShellOptions>>();
            var shellSettings = serviceProvider.GetRequiredService<ShellSettings>();

            var contentRoot = GetContentRoot(shellOptions.Value, shellSettings);

            if (!Directory.Exists(contentRoot))
            {
                Directory.CreateDirectory(contentRoot);
            }
            return new TenantFileProvider(contentRoot);
        });

        services.AddSingleton<IStaticFileProvider>(serviceProvider =>
        {
            return serviceProvider.GetRequiredService<ITenantFileProvider>();
        });
    }

    public override void Configure(IApplicationBuilder app, IEndpointRouteBuilder routes, IServiceProvider serviceProvider)
    {
        var tenantFileProvider = serviceProvider.GetRequiredService<ITenantFileProvider>();

        app.UseStaticFiles(new StaticFileOptions
        {
            FileProvider = tenantFileProvider,
            DefaultContentType = MediaTypeNames.Application.Octet,
            ServeUnknownFileTypes = true,

            // Cache the tenant static files for 30 days.
            OnPrepareResponse = ctx =>
            {
                ctx.Context.Response.Headers[HeaderNames.CacheControl] = $"public, max-age={TimeSpan.FromDays(30).TotalSeconds}, s-max-age={TimeSpan.FromDays(365.25).TotalSeconds}";
            },
        });
    }

    private static string GetContentRoot(ShellOptions shellOptions, ShellSettings shellSettings) =>
        Path.Combine(shellOptions.ShellsApplicationDataPath, shellOptions.ShellsContainerName, shellSettings.Name, AssetsPath);
}

[Feature("Grove.Infrastructure.Tenants.Distributed")]
public sealed class DistributedStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddSingleton<DistributedShellMarkerService>();
    }
}

[Feature("Grove.Infrastructure.Tenants.FeatureProfiles")]
public sealed class FeatureProfilesStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddScoped<FeatureProfilesManager>();
        services.AddScoped<IFeatureProfilesService, FeatureProfilesService>();

        // The feature profiles page (Grove.Themes.Admin.Client, /TenantFeatureProfiles) and its API.
        services.AddApiContributor<Api.FeatureProfilesApi>();

        services.AddRecipeExecutionStep<FeatureProfilesStep>();
    }
}

[RequireFeatures("Grove.Deployment", "Grove.Infrastructure.Tenants.FeatureProfiles")]
public sealed class FeatureProfilesDeploymentStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddDeployment<AllFeatureProfilesDeploymentSource, AllFeatureProfilesDeploymentStep>();
    }
}
