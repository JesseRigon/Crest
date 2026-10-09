using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Crest.DisplayManagement.Handlers;
using Crest.Modules;
using Crest.Navigation;
using Crest.Recipes;
using Crest.Security.Permissions;
using Crest.UrlRewriting.Drivers;
using Crest.UrlRewriting.Endpoints.Rules;
using Crest.UrlRewriting.Extensions;
using Crest.UrlRewriting.Handlers;
using Crest.UrlRewriting.Models;
using Crest.UrlRewriting.Recipes;
using Crest.UrlRewriting.Services;

namespace Crest.UrlRewriting;

public sealed class Startup : StartupBase
{
    public override int Order
        => PlatformConstants.ConfigureOrder.UrlRewriting;

    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddUrlRewritingServices()
            .AddNavigationProvider<AdminMenu>()
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

[RequireFeatures("Crest.Recipes.Core")]
public sealed class RecipesStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddRecipeExecutionStep<UrlRewritingStep>();
    }
}
