using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Http.Resilience;
using Microsoft.Extensions.Options;
using Crest.DisplayManagement.Descriptors;
using Crest.Environment.Options;
using Crest.ReCaptcha.Configuration;
using Crest.ReCaptcha.Services;
using Crest.ReCaptcha.TagHelpers;
using Polly;

namespace Crest.ReCaptcha.Core;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddReCaptcha(this IServiceCollection services, Action<ReCaptchaSettings> configure = null)
    {
        // c.f. https://learn.microsoft.com/en-us/dotnet/architecture/microservices/implement-resilient-applications/use-httpclientfactory-to-implement-resilient-http-requests
        services.AddSingleton<ReCaptchaService>();
        services.AddShapeAttributes<ReCaptchaShape>();
        services.AddSignalOptionsChangeTokenSource<ReCaptchaSettings>();
        services
            .AddHttpClient(nameof(ReCaptchaService))
            .AddResilienceHandler("oc-handler", builder => builder
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

        services.AddTransient<IConfigureOptions<ReCaptchaSettings>, ReCaptchaSettingsConfiguration>();

        services.AddTagHelpers<ReCaptchaTagHelper>();

        if (configure != null)
        {
            services.Configure(configure);
        }

        return services;
    }
}
