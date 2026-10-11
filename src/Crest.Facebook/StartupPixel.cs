using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Http.Resilience;
using Crest.DisplayManagement.Handlers;
using Crest.Facebook.Drivers;
using Crest.Facebook.Filters;
using Crest.Facebook.Services;
using Crest.Modules;
using Crest.Navigation;
using Crest.Security.Permissions;
using Crest.Facebook.Workflows;
using Crest.Workflows;
using Crest.Workflows.Extensions;
using Polly;

namespace Crest.Facebook;

[Feature(FacebookConstants.Features.Pixel)]
public sealed class StartupPixel : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddSiteDisplayDriver<FacebookPixelSettingsDisplayDriver>();
        services.AddPermissionProvider<PixelPermissionProvider>();
        services.AddNavigationProvider<AdminMenuPixel>();

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
        services.ConfigureCrestWorkflows(workflows => workflows.AddActivitiesFrom<StartupPixelWorkflows>());
    }
}

