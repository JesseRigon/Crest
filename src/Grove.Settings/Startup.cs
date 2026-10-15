using Fluid;
using Fluid.Values;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Grove.Deployment;
using Grove.DisplayManagement;
using Grove.DisplayManagement.Handlers;
using Grove.Environment.Options;
using Grove.Environment.Shell.Configuration;
using Grove.Environment.Shell.Scope;
using Grove.Liquid;
using Grove.Modules;
using Grove.Navigation;
using Grove.Recipes;
using Grove.Recipes.Services;
using Grove.ResourceManagement;
using Grove.Roles;
using Grove.Security.Permissions;
using Grove.Settings.Deployment;
using Grove.Settings.Recipes;
using Grove.Settings.Services;
using Grove.Setup.Events;

namespace Grove.Settings;

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
            .AddScoped<Grove.Access.IResourcePermissionMapper, SiteSettingsPermissionMapper>();

        services.AddRecipeExecutionStep<SettingsStep>();
        services.AddSingleton<ISiteService, SiteService>();
        services.AddScoped<SiteIdentity>();
        services.AddApiContributor<Api.SiteSettingsApi>();
        services.AddApiContributor<Api.BrandingSettingsApi>();
        services.AddApiContributor<Api.DebugSettingsApi>();

        // The settings groups the generic settings form serves (docs/MVC/parity.md › SettingsForm).
        services.AddScoped<Groups.SettingsGroupService>();
        services.AddApiContributor<Api.SettingsGroupsApi>();
        services.AddPageSource<Groups.SettingsGroupPages>();

        // The branding renditions the shipped surfaces consume (docs/branding.md); a module
        // that adds a surface registers its own the same way.
        services.AddBrandingRendition(new BrandingRendition("login", "Login logo", "The login page's header, beside or instead of the site name", Width: 320, Height: 96));
        services.AddBrandingRendition(new BrandingRendition("sidebar", "Sidebar logo", "The admin and member navigation header and the tenant chip in the switcher", Width: 64, Height: 64));
        services.AddBrandingRendition(new BrandingRendition("favicon", "Favicon", "The browser tab icon", Width: 32, Height: 32, Accept: ".ico,.png,.svg"));
        services.AddBrandingRendition(new BrandingRendition("email", "Email logo", "Mail templates", Width: 240, Height: 64, Reserved: true));

        // Site Settings editor
        services.AddSiteSettingsPermission(SiteSettings.GroupId, SettingsPermissions.ManageGeneralSettings);
        services.AddSiteSettingsPermission(DebugSettings.GroupId, SettingsPermissions.ManageDebuggingSettings);

        services.AddScoped<ITimeZoneSelector, DefaultTimeZoneSelector>();

        services.AddDeployment<SiteSettingsDeploymentSource, SiteSettingsDeploymentStep>();

        services.AddScoped<IRecipeEnvironmentProvider, RecipeEnvironmentSiteNameProvider>();
        services.AddSignalOptionsChangeTokenSource<ShapeRenderingOptions>();

        services.AddTransient<IPostConfigureOptions<ResourceOptions>, ResourceOptionsConfiguration>();
        services.AddTransient<IPostConfigureOptions<PagerOptions>, PagerOptionsConfiguration>();
        services.AddTransient<IConfigureOptions<ShapeRenderingOptions>, ShapeRenderingOptionsConfiguration>();

        services.AddScoped<IModularTenantEvents, PreloadSiteSettingsTenantEventHandler>();
    }
}

[RequireFeatures("Grove.Liquid.Core")]
public sealed class LiquidStartup : StartupBase
{
    private readonly IShellConfiguration _configuration;

    public LiquidStartup(IShellConfiguration configuration) =>
        _configuration = configuration;

    public override void ConfigureServices(IServiceCollection services)
    {
        services.Configure<SettingsLiquidOptions>(_configuration.GetSection("Grove_Settings_Liquid"));
        
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

[RequireFeatures("Grove.Deployment")]
public sealed class DeploymentStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddSiteSettingsPropertyDeploymentStep<DebugSettings>();
    }
}
