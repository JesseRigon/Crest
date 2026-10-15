using System.ComponentModel.DataAnnotations;
using System.Diagnostics;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using OpenIddict.Server;
using OpenIddict.Server.AspNetCore;
using OpenIddict.Server.DataProtection;
using OpenIddict.Validation;
using OpenIddict.Validation.AspNetCore;
using OpenIddict.Validation.DataProtection;
using Grove.BackgroundTasks;
using Grove.Data.Migration;
using Grove.Deployment;
using Grove.Display.Management.Handlers;
using Grove.Environment.Shell.Builders;
using Grove.Data.Localization;
using Grove.Modules;
using Grove.Display.Navigation;
using Grove.Access.OpenId.Configuration;
using Grove.Access.OpenId.Deployment;
using Grove.Access.OpenId.Handlers;
using Grove.Access.OpenId.Migrations;
using Grove.Access.OpenId.Recipes;
using Grove.Access.OpenId.Services;
using Grove.Access.OpenId.Services.Handlers;
using Grove.Access.OpenId.Settings;
using Grove.Access.OpenId.Tasks;
using Grove.Infrastructure.Recipes;
using Grove.Security.RateLimits;
using Grove.Security;
using Grove.Security.Permissions;
using Grove.Access.Users;

namespace Grove.Access.OpenId;

public sealed class Startup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        // Register the OpenIddict core services and the Grove migrations, managers and default YesSql stores.
        // The default YesSql stores can be replaced by another database by referencing the corresponding
        // OpenIddict package (e.g OpenIddict.EntityFrameworkCore) and registering it in the options.
        services.AddOpenIddict()
            .AddCore(options =>
            {
                options.AddPlatformMigrations()
                       .UsePlatformManagers()
                       .UseYesSql();
            });

        services.AddPermissionProvider<Permissions>();
    }
}

[Feature(OpenIdConstants.Features.Client)]
public sealed class ClientStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddSettingsGroup<OpenIdClientSettings>("Grove.Access.OpenId.Client", "Authentication Client", OpenIdPermissions.ManageClientSettings, group => group
            .InMenu("openid-settings", entryId: "openid-client")
            .ReloadsTenant()
            .ValidatedBy(async (provider, settings, errors, _) =>
            {
                foreach (var result in await provider.GetRequiredService<IOpenIdClientService>().ValidateSettingsAsync(settings))
                {
                    if (result != System.ComponentModel.DataAnnotations.ValidationResult.Success)
                    {
                        errors.Add(result.MemberNames.FirstOrDefault() ?? string.Empty, result.ErrorMessage ?? string.Empty);
                    }
                }
            }));
        services.AddScoped<IJSLocalizer, OpenIdJSLocalizer>();

        services.TryAddSingleton<IOpenIdClientService, OpenIdClientService>();

        // Note: the following services are registered using TryAddEnumerable to prevent duplicate registrations.

        services.AddRecipeExecutionStep<OpenIdClientSettingsStep>();
        // Register the options initializers required by the OpenID Connect client handler.
        services.TryAddEnumerable(new[]
        {
            // Grove-specific initializers:
            ServiceDescriptor.Singleton<IConfigureOptions<AuthenticationOptions>, OpenIdClientConfiguration>(),
            ServiceDescriptor.Singleton<IConfigureOptions<OpenIdConnectOptions>, OpenIdClientConfiguration>(),

            // Built-in initializers:
            ServiceDescriptor.Singleton<IPostConfigureOptions<OpenIdConnectOptions>, OpenIdConnectPostConfigureOptions>(),
        });
    }
}

[Feature(OpenIdConstants.Features.Server)]
public sealed class ServerStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {

        services.AddOpenIddict()
            .AddServer(options =>
            {
                options.UseAspNetCore();
                options.UseDataProtection();
                options.AddEventHandler(PersistStoresHandler.Descriptor);
            });

        services.TryAddSingleton<IOpenIdServerService, OpenIdServerService>();

        // The protocol endpoints (/connect/*), the server configuration and the consent pages' data.
        services.AddApiContributor<Api.OpenIdProtocolApi>();
        services.AddApiContributor<Api.OpenIdServerConfigurationApi>();
        services.AddApiContributor<Api.OpenIdConsentApi>();

        services.AddDataMigration<DefaultScopesMigration>();
        services.AddDataMigration<PushedAuthorizationRequestsMigration>();


        // Note: the following services are registered using TryAddEnumerable to prevent duplicate registrations.
        services.TryAddEnumerable(new[]
        {
            ServiceDescriptor.Scoped<IRoleRemovedEventHandler, OpenIdApplicationRoleRemovedEventHandler>(),
            ServiceDescriptor.Singleton<IBackgroundTask, OpenIdBackgroundTask>(),
        });

        services.AddRecipeExecutionStep<OpenIdServerSettingsStep>()
            .AddRecipeExecutionStep<OpenIdApplicationStep>()
            .AddRecipeExecutionStep<OpenIdScopeStep>();

        // Note: the OpenIddict ASP.NET host adds an authentication options initializer that takes care of
        // registering the server ASP.NET Core handler. Yet, it MUST NOT be registered at this stage
        // as it is lazily registered by OpenIdServerConfiguration only after checking the OpenID server
        // settings are valid and can be safely used in this tenant without causing runtime exceptions.
        // To prevent that, the initializer is manually removed from the services collection of the tenant.
        services.RemoveAll<IConfigureOptions<AuthenticationOptions>, OpenIddictServerAspNetCoreConfiguration>();

        services.TryAddEnumerable(new[]
        {
            ServiceDescriptor.Singleton<IConfigureOptions<AuthenticationOptions>, OpenIdServerConfiguration>(),
            ServiceDescriptor.Singleton<IConfigureOptions<OpenIddictServerOptions>, OpenIdServerConfiguration>(),
            ServiceDescriptor.Singleton<IConfigureOptions<OpenIddictServerAspNetCoreOptions>, OpenIdServerConfiguration>(),
            ServiceDescriptor.Singleton<IConfigureOptions<OpenIddictServerDataProtectionOptions>, OpenIdServerConfiguration>(),
        });
    }
}

[Feature(OpenIdConstants.Features.Server)]
[RequireFeatures("Grove.Security.RateLimits")]
public sealed class ServerRateLimitsStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.Configure<RateLimitsOptions>(options =>
            options.AddRouteRateLimit("Access.Token", HttpMethods.Post, RateLimitPartitionHelpers.CreateSlidingWindowPerIpPolicy(UserRateLimiterPolicyNames.PasswordAuthentication, 10)));
    }
}

[RequireFeatures("Grove.Deployment", OpenIdConstants.Features.Server)]
public sealed class ServerDeploymentStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddDeployment<OpenIdServerDeploymentSource, OpenIdServerDeploymentStep>();
    }
}

[Feature(OpenIdConstants.Features.Validation)]
public sealed class ValidationStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {

        // Used to attach RFC 9457 Problem Details bodies to the challenge responses produced
        // by the OpenIddict validation handler, honoring app-level ProblemDetails customizations.
        services.AddProblemDetails();

        services.AddOpenIddict()
            .AddValidation(options =>
            {
                options.UseAspNetCore();
                options.UseDataProtection();
                options.UseSystemNetHttp();
            });

        services.TryAddSingleton<IOpenIdValidationService, OpenIdValidationService>();
        services.AddApiContributor<Api.OpenIdValidationConfigurationApi>();

        // Note: the following services are registered using TryAddEnumerable to prevent duplicate registrations.

        services.AddRecipeExecutionStep<OpenIdValidationSettingsStep>();

        // Note: the OpenIddict ASP.NET host adds an authentication options initializer that takes care of
        // registering the validation handler. Yet, it MUST NOT be registered at this stage as it is
        // lazily registered by OpenIdValidationConfiguration only after checking the OpenID validation
        // settings are valid and can be safely used in this tenant without causing runtime exceptions.
        // To prevent that, the initializer is manually removed from the services collection of the tenant.
        services.RemoveAll<IConfigureOptions<AuthenticationOptions>, OpenIddictValidationAspNetCoreConfiguration>();

        services.TryAddEnumerable(new[]
        {
            ServiceDescriptor.Singleton<IConfigureOptions<AuthenticationOptions>, OpenIdValidationConfiguration>(),
            ServiceDescriptor.Singleton<IConfigureOptions<ApiAuthorizationOptions>, OpenIdValidationConfiguration>(),
            ServiceDescriptor.Singleton<IConfigureOptions<OpenIddictValidationOptions>, OpenIdValidationConfiguration>(),
            ServiceDescriptor.Singleton<IConfigureOptions<OpenIddictValidationDataProtectionOptions>, OpenIdValidationConfiguration>(),
        });
    }
}

[RequireFeatures("Grove.Deployment", OpenIdConstants.Features.Validation)]
public sealed class ValidationDeploymentStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddDeployment<OpenIdValidationDeploymentSource, OpenIdValidationDeploymentStep>();
    }
}

[Feature(OpenIdConstants.Features.Management)]
public sealed class ManagementStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddApiContributor<Api.OpenIdApplicationsApi>();
        services.AddApiContributor<Api.OpenIdScopesApi>();
    }
}

internal static class OpenIdServiceCollectionExtensions
{
    public static IServiceCollection RemoveAll(this IServiceCollection services, Type serviceType, Type implementationType)
    {
        Debug.Assert(services != null, "The services collection shouldn't be null.");
        Debug.Assert(serviceType != null, "The service type shouldn't be null.");
        Debug.Assert(implementationType != null, "The implementation type shouldn't be null.");

        for (var index = services.Count - 1; index >= 0; index--)
        {
            var descriptor = services[index];
            if (descriptor.ServiceType == serviceType && descriptor.GetImplementationType() == implementationType)
            {
                services.RemoveAt(index);
            }
        }

        return services;
    }

    public static IServiceCollection RemoveAll<TService, TImplementation>(this IServiceCollection services)
        where TImplementation : TService
        => services.RemoveAll(typeof(TService), typeof(TImplementation));
}
