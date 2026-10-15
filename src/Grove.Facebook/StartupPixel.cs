using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Http.Resilience;
using Grove.DisplayManagement.Handlers;
using Grove.Facebook.Filters;
using Grove.Facebook.Services;
using Grove.Facebook.Settings;
using Grove.Modules;
using Grove.Navigation;
using Grove.Security.Permissions;
using Grove.Facebook.Workflows;
using Grove.Workflows;
using Grove.Workflows.Extensions;
using Polly;

namespace Grove.Facebook;

[Feature(FacebookConstants.Features.Pixel)]
public sealed class StartupPixel : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddSettingsGroup<FacebookPixelSettings>(
            FacebookConstants.PixelSettingsGroupId,
            "Meta Pixel",
            FacebookConstants.ManageFacebookPixelPermission,
            group => group.InMenu("settings-integrations", entryId: "facebookPixel").AlsoAdmits(Permissions.ManageFacebookApp));
        services.AddPermissionProvider<PixelPermissionProvider>();

        services.Configure<MvcOptions>((options) =>
        {
            options.Filters.Add<FacebookPixelFilter>();
        });

        services.AddHttpClient<IMetaConversionsApiService, MetaConversionsApiService>(client =>
        {
            client.BaseAddress = new Uri("https://graph.facebook.com/");
        }).AddResilienceHandler("oc-handler", builder => builder
            .AddRetry(new HttpRetryStrategyOptions
            {
                Name = "oc-retry",
                MaxRetryAttempts = 3,
                OnRetry = attempt =>
                {
                    attempt.RetryDelay.Add(TimeSpan.FromSeconds(0.5 * attempt.AttemptNumber));

                    return ValueTask.CompletedTask;
                },
            })
        );
    }
}

[Feature(FacebookConstants.Features.Pixel)]
[RequireFeatures(WorkflowsConstants.FeatureId)]
public sealed class StartupPixelWorkflows : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddScoped<IWorkflowActivityProvider, FacebookWorkflowProvider>();
        services.ConfigureGroveWorkflows(workflows => workflows.AddActivitiesFrom<StartupPixelWorkflows>());
    }
}

