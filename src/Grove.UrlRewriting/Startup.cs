using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Grove.DisplayManagement.Handlers;
using Grove.Modules;
using Grove.Navigation;
using Grove.Recipes;
using Grove.Security.Permissions;
using Grove.UrlRewriting.Drivers;
using Grove.UrlRewriting.Endpoints.Rules;
using Grove.UrlRewriting.Extensions;
using Grove.UrlRewriting.Handlers;
using Grove.UrlRewriting.Models;
using Grove.UrlRewriting.Recipes;
using Grove.UrlRewriting.Services;

namespace Grove.UrlRewriting;

public sealed class Startup : StartupBase
{
    public override int Order
        => PlatformConstants.ConfigureOrder.UrlRewriting;

    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddUrlRewritingServices()
            .AddPermissionProvider<UrlRewritingPermissionProvider>()
            .AddResourceConfiguration<ResourceManagementOptionsConfiguration>()
            .AddDisplayDriver<RewriteRule, RewriteRulesDisplayDriver>();

        // Add Apache Mod Redirect Rule.
        services.AddRewriteRuleSource<UrlRedirectRuleSource>(UrlRedirectRuleSource.SourceName)
            .AddScoped<IRewriteRuleHandler, UrlRedirectRuleHandler>()
            .AddDisplayDriver<RewriteRule, UrlRedirectRuleDisplayDriver>();

        // Add Apache Mod Rewrite Rule.
        services.AddRewriteRuleSource<UrlRewriteRuleSource>(UrlRewriteRuleSource.SourceName)
            .AddScoped<IRewriteRuleHandler, UrlRewriteRuleHandler>()
            .AddDisplayDriver<RewriteRule, UrlRewriteRuleDisplayDriver>();
    }

    public override void Configure(IApplicationBuilder app, IEndpointRouteBuilder routes, IServiceProvider serviceProvider)
    {
        routes.AddSortRulesEndpoint();

        app.UseUrlRewriting(serviceProvider);
    }
}

[RequireFeatures("Grove.Recipes.Core")]
public sealed class RecipesStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddRecipeExecutionStep<UrlRewritingStep>();
    }
}
