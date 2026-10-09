using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Crest.Data.Migration;
using Crest.Deployment;
using Crest.DisplayManagement.Handlers;
using Crest.Modules;
using Crest.Navigation;
using Crest.RateLimits.Core;
using Crest.RateLimits.Deployment;
using Crest.RateLimits.Drivers;
using Crest.RateLimits.Migrations;
using Crest.RateLimits.Models;
using Crest.RateLimits.Recipes;
using Crest.RateLimits.Services;
using Crest.Recipes;
using Crest.Security.Permissions;

namespace Crest.RateLimits;

public sealed class Startup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddRateLimiter();
        services.AddDataMigration<GlobalRateLimitsMigrations>();
        services.AddTransient<IConfigureOptions<RateLimiterOptions>, RateLimiterOptionsConfigurations>();
        services.AddSingleton<IRateLimitPolicyStore, RateLimitPolicyStore>();
        services.AddDisplayDriver<RateLimitLimiter, RateLimitLimiterDisplayDriver>();
        services.AddDisplayDriver<RateLimitPolicy, RateLimitPolicyDisplayDriver>();

        services.AddKeyedSingleton<IRateLimiterSource, FixedWindowRateLimiterSource>(FixedWindowRateLimiterSource.SourceName)
            .AddDisplayDriver<RateLimitLimiter, FixedWindowRateLimiterDisplayDriver>();

        services.AddKeyedSingleton<IRateLimiterSource, SlidingWindowRateLimiterSource>(SlidingWindowRateLimiterSource.SourceName)
            .AddDisplayDriver<RateLimitLimiter, SlidingWindowRateLimiterDisplayDriver>();

        services.AddKeyedSingleton<IRateLimiterSource, ConcurrencyRateLimiterSource>(ConcurrencyRateLimiterSource.SourceName)
            .AddDisplayDriver<RateLimitLimiter, ConcurrencyRateLimiterDisplayDriver>();

        services.AddKeyedSingleton<IRateLimiterSource, TokenBucketRateLimiterSource>(TokenBucketRateLimiterSource.SourceName)
            .AddDisplayDriver<RateLimitLimiter, TokenBucketRateLimiterDisplayDriver>();

        services.AddNavigationProvider<AdminMenu>();
        services.AddPermissionProvider<Permissions>();
    }

    public override void Configure(IApplicationBuilder app, IEndpointRouteBuilder routes, IServiceProvider serviceProvider)
    {
        app.UseRateLimiter();
    }
}

[RequireFeatures("Crest.Recipes.Core")]
public sealed class RecipesStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddRecipeExecutionStep<CreateOrUpdateRateLimitPoliciesStep>();
    }
}

[RequireFeatures("Crest.Deployment")]
public sealed class DeploymentStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddDeployment<AllRateLimitPoliciesDeploymentSource, AllRateLimitPoliciesDeploymentStep, AllRateLimitPoliciesDeploymentStepDriver>();
    }
}
