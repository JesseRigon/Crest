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
using Grove.RateLimits.Migrations;
using Grove.RateLimits.Models;
using Grove.RateLimits.Recipes;
using Grove.RateLimits.Services;
using Grove.RateLimits.Settings;
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
        services.AddDeployment<AllRateLimitPoliciesDeploymentSource, AllRateLimitPoliciesDeploymentStep>();
    }
}
