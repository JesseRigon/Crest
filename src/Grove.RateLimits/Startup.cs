using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Grove.Data.Migration;
using Grove.Deployment;
using Grove.DisplayManagement.Handlers;
using Grove.Modules;
using Grove.Navigation;
using Grove.RateLimits;
using Grove.RateLimits.Deployment;
using Grove.RateLimits.Drivers;
using Grove.RateLimits.Migrations;
using Grove.RateLimits.Models;
using Grove.RateLimits.Recipes;
using Grove.RateLimits.Services;
using Grove.Recipes;
using Grove.Security.Permissions;

namespace Grove.RateLimits;

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

        services.AddPageSource<AdminPages>();
        services.AddPermissionProvider<Permissions>();
    }

    public override void Configure(IApplicationBuilder app, IEndpointRouteBuilder routes, IServiceProvider serviceProvider)
    {
        app.UseRateLimiter();
    }
}

[RequireFeatures("Grove.Recipes.Core")]
public sealed class RecipesStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddRecipeExecutionStep<CreateOrUpdateRateLimitPoliciesStep>();
    }
}

[RequireFeatures("Grove.Deployment")]
public sealed class DeploymentStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddDeployment<AllRateLimitPoliciesDeploymentSource, AllRateLimitPoliciesDeploymentStep, AllRateLimitPoliciesDeploymentStepDriver>();
    }
}
