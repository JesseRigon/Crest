using Fluid;
using Fluid.Values;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Crest.Deployment;
using Crest.DisplayManagement;
using Crest.DisplayManagement.Handlers;
using Crest.Environment.Options;
using Crest.Environment.Shell.Configuration;
using Crest.Environment.Shell.Scope;
using Crest.Liquid;
using Crest.Modules;
using Crest.Navigation;
using Crest.Recipes;
using Crest.Recipes.Services;
using Crest.ResourceManagement;
using Crest.Roles;
using Crest.Security.Permissions;
using Crest.Settings.Deployment;
using Crest.Settings.Drivers;
using Crest.Settings.Recipes;
using Crest.Settings.Services;
using Crest.Setup.Events;

namespace Crest.Settings;

/// <summary>
/// These services are registered on the tenant service collection.
/// </summary>
public sealed class Startup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddScoped<ISetupEventHandler, SetupEventHandler>();
        services.AddPermissionProvider<Permissions>();

        services.AddRolesCoreServices()
            .AddScoped<Crest.Access.IResourcePermissionMapper, SiteSettingsPermissionMapper>();

        services.AddRecipeExecutionStep<SettingsStep>();
        services.AddSingleton<ISiteService, SiteService>();

        // Site Settings editor
        services.AddSiteDisplayDriver<DefaultSiteSettingsDisplayDriver>();
        services.AddSiteDisplayDriver<DebugSettingsDisplayDriver>();
        services.AddSiteDisplayDriver<ButtonsSettingsDisplayDriver>();
        services.AddSiteSettingsPermission(DefaultSiteSettingsDisplayDriver.GroupId, SettingsPermissions.ManageGeneralSettings);
        services.AddSiteSettingsPermission(DebugSettingsDisplayDriver.GroupId, SettingsPermissions.ManageDebuggingSettings);
        services.AddNavigationProvider<AdminMenu>();

        services.AddScoped<ITimeZoneSelector, DefaultTimeZoneSelector>();

        services.AddDeployment<SiteSettingsDeploymentSource, SiteSettingsDeploymentStep, SiteSettingsDeploymentStepDriver>();

        services.AddScoped<IRecipeEnvironmentProvider, RecipeEnvironmentSiteNameProvider>();
        services.AddSignalOptionsChangeTokenSource<ShapeRenderingOptions>();

        services.AddTransient<IPostConfigureOptions<ResourceOptions>, ResourceOptionsConfiguration>();
        services.AddTransient<IPostConfigureOptions<PagerOptions>, PagerOptionsConfiguration>();
        services.AddTransient<IConfigureOptions<ShapeRenderingOptions>, ShapeRenderingOptionsConfiguration>();

        services.AddScoped<IModularTenantEvents, PreloadSiteSettingsTenantEventHandler>();
    }
}

[RequireFeatures("Crest.Liquid.Core")]
public sealed class LiquidStartup : StartupBase
{
    private readonly IShellConfiguration _configuration;

    public LiquidStartup(IShellConfiguration configuration) =>
        _configuration = configuration;

    public override void ConfigureServices(IServiceCollection services)
    {
        services.Configure<SettingsLiquidOptions>(_configuration.GetSection("Crest_Settings_Liquid"));
        
        services.AddSingleton<ISitePropertiesLiquidMapper, SitePropertiesLiquidMapper>();
        services.Configure<TemplateOptions>(o =>
        {
            o.Scope.SetValue("Site", new ObjectValue(new LiquidSiteSettingsAccessor()));
            o.MemberAccessStrategy.Register<LiquidSiteSettingsAccessor, FluidValue>(async (obj, name, context) =>
            {
                var liquidTemplateContext = (LiquidTemplateContext)context;
                var services = liquidTemplateContext.Services;

                var siteService = services.GetRequiredService<ISiteService>();
                var site = await siteService.GetSiteSettingsAsync();

                FluidValue result = name switch
                {
                    nameof(ISite.SiteName) => new StringValue(site.SiteName),
                    nameof(ISite.PageTitleFormat) => new StringValue(site.PageTitleFormat),
                    // The site salt should never be accessible to Liquid and exposing it is a major security risk. This
                    // comment and the dummy value below should be kept, to record that it's intentional.
                    nameof(ISite.SiteSalt) => new StringValue("[REDACTED]"),
                    nameof(ISite.SuperUser) => new StringValue(site.SuperUser),
                    nameof(ISite.Calendar) => new StringValue(site.Calendar),
                    nameof(ISite.TimeZoneId) => new StringValue(site.TimeZoneId),
                    nameof(ISite.ResourceDebugMode) => new StringValue(site.ResourceDebugMode.ToString()),
                    nameof(ISite.UseCdn) => BooleanValue.Create(site.UseCdn),
                    nameof(ISite.CdnBaseUrl) => new StringValue(site.CdnBaseUrl),
                    nameof(ISite.PageSize) => NumberValue.Create(site.PageSize),
                    nameof(ISite.MaxPageSize) => NumberValue.Create(site.MaxPageSize),
                    nameof(ISite.MaxPagedCount) => NumberValue.Create(site.MaxPagedCount),
                    nameof(ISite.BaseUrl) => new StringValue(site.BaseUrl),
                    nameof(ISite.HomeRoute) => new ObjectValue(site.HomeRoute),
                    nameof(ISite.AppendVersion) => BooleanValue.Create(site.AppendVersion),
                    nameof(ISite.CacheMode) => new StringValue(site.CacheMode.ToString()),
                    nameof(ISite.Properties) => await services.GetRequiredService<ISitePropertiesLiquidMapper>().MapAsync(site),
                    _ => NilValue.Instance
                };

                return result;
            });
        });
    }
}

[RequireFeatures("Crest.Deployment")]
public sealed class DeploymentStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddSiteSettingsPropertyDeploymentStep<DebugSettings, DeploymentStartup>(
            S => S["Debugging settings"],
            S => S["Exports the debugging settings."]);
    }
}
