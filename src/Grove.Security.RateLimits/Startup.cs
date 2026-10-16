using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Grove.Data.Migration;
using Grove.Deployment;
using Grove.Display.Management.Handlers;
using Grove.Modules;
using Grove.Display.Navigation;
using Grove.Security.RateLimits;
using Grove.Security.RateLimits.Deployment;
using Grove.Security.RateLimits.Migrations;
using Grove.Security.RateLimits.Models;
using Grove.Security.RateLimits.Recipes;
using Grove.Security.RateLimits.Services;
using Grove.Security.RateLimits.Settings;
using Grove.Infrastructure.Recipes;
using Grove.Security.Permissions;

namespace Grove.Security.RateLimits;

public sealed class Startup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddShellRateLimiter();
        services.AddDataMigration<GlobalRateLimitsMigrations>();
        services.AddTransient<IConfigureOptions<RateLimiterOptions>, RateLimiterOptionsConfigurations>();
        services.AddSingleton<IRateLimitPolicyStore, RateLimitPolicyStore>();

        services.AddKeyedSingleton<IRateLimiterSource, FixedWindowRateLimiterSource>(FixedWindowRateLimiterSource.SourceName);

        services.AddKeyedSingleton<IRateLimiterSource, SlidingWindowRateLimiterSource>(SlidingWindowRateLimiterSource.SourceName);

        services.AddKeyedSingleton<IRateLimiterSource, ConcurrencyRateLimiterSource>(ConcurrencyRateLimiterSource.SourceName);

        services.AddKeyedSingleton<IRateLimiterSource, TokenBucketRateLimiterSource>(TokenBucketRateLimiterSource.SourceName);

        services.AddPermissionProvider<Permissions>();
        services.AddSettingsGroup<RateLimitsSettingsModel>(
            RateLimitsSettingsModel.GroupId,
            "Rate Limits",
            RateLimitsPermissions.ManageRateLimits,
            group => group
                .InMenu("tools", entryId: "rate-limits")
                .StoredBy(
                    (services, _) => RateLimitsSettingsStore.LoadAsync(services),
                    (services, model, _) => RateLimitsSettingsStore.SaveAsync(services, model))
                .ValidatedBy((services, model, errors, _) => RateLimitsSettingsStore.ValidateAsync(services, model, errors)));
    }

    public override void Configure(IApplicationBuilder app, IEndpointRouteBuilder routes, IServiceProvider serviceProvider)
    {
        app.UseShellRateLimiter();
    }
}

[RequireFeatures("Grove.Infrastructure.Recipes.Core")]
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
        services.AddDeployment<AllRateLimitPoliciesDeploymentSource, AllRateLimitPoliciesDeploymentStep>();
    }
}
