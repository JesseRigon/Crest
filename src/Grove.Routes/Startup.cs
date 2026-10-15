using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Grove.Modules;
using Grove.Infrastructure.Recipes;
using Grove.Security.Permissions;
using Grove.Routes.Api;
using Grove.Routes.ContentRoutes;
using Grove.Routes.Extensions;
using Grove.Routes.Handlers;
using Grove.Routes.Recipes;
using Grove.Routes.Services;

namespace Grove.Routes;

public sealed class Startup : StartupBase
{
    public override int Order
        => PlatformConstants.ConfigureOrder.UrlRewriting;

    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddUrlRewritingServices()
            .AddPermissionProvider<UrlRewritingPermissionProvider>();

        // The redirect rule (Apache mod_rewrite R flag) and the rewrite rule.
        services.AddRewriteRuleSource<UrlRedirectRuleSource>(UrlRedirectRuleSource.SourceName)
            .AddScoped<IRewriteRuleHandler, UrlRedirectRuleHandler>();

        services.AddRewriteRuleSource<UrlRewriteRuleSource>(UrlRewriteRuleSource.SourceName)
            .AddScoped<IRewriteRuleHandler, UrlRewriteRuleHandler>();

        // The route management page (Grove.Routes.BlazorWasm) and its API.
        services.AddApiContributor<RoutesApi>();
    }

    public override void Configure(IApplicationBuilder app, IEndpointRouteBuilder routes, IServiceProvider serviceProvider)
    {
        app.UseUrlRewriting(serviceProvider);
    }
}

[RequireFeatures("Grove.Routes.Auto")]
public sealed class AutorouteStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services) =>
        services.AddScoped<IContentRouteSource, AutorouteContentRouteSource>();
}

[RequireFeatures("Grove.Contents.Alias")]
public sealed class AliasStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services) =>
        services.AddScoped<IContentRouteSource, AliasContentRouteSource>();
}

[RequireFeatures("Grove.Infrastructure.Recipes.Core")]
public sealed class RecipesStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddRecipeExecutionStep<UrlRewritingStep>();
    }
}
