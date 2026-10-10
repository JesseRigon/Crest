using Crest.Workflows.Studio.Contracts;
using Crest.Workflows.Studio.Extensions;
using Crest.Workflows.Studio.Localization.Time;
using Crest.Workflows.Studio.Localization.Time.Providers;
using Crest.Workflows.Studio.Models;
using Crest.Workflows.Studio.Shell.Extensions;
using Crest.Workflows.Studio.Workflows.Designer.Extensions;
using Crest.Workflows.Studio.Workflows.Extensions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Crest.Workflows.Designer.Services;

namespace Crest.Workflows.Designer.Extensions;

/// <summary>
/// Extension methods for configuring the Crest.Workflows Designer services shared across hosting models.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Configures the shared Crest.Workflows Designer services and allows platform specific configuration hooks.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configuration">Application configuration.</param>
    /// <param name="configurePlatformServices">Callback invoked to register platform specific services.</param>
    /// <returns>The service collection.</returns>
    public static IServiceCollection AddCrestWorkflowsDesignerCore(this IServiceCollection services, IConfiguration configuration, Action<IServiceCollection> configurePlatformServices)
    {
        services.AddSingleton<BackendService>();

        var backendApiConfig = new BackendApiConfig
        {
            ConfigureBackendOptions = options => configuration.GetSection("Backend").Bind(options)
        };

        configurePlatformServices(services);

        services.AddShell();
        services.AddRemoteBackend(backendApiConfig);
        services.AddScoped<IAuthenticationProviderManager, NoopAuthenticationProviderManager>();
        services.Replace(ServiceDescriptor.Scoped<IRemoteBackendAccessor, ComponentRemoteBackendAccessor>());
        services.AddWorkflowsModule();
        services.AddScoped<ITimeZoneProvider, LocalTimeZoneProvider>();
        services.AddActivityDisplaySettingsProvider<ActivityIconProvider>();

        return services;
    }
}

/// <summary>
/// A no-op authentication provider manager for use in embedded scenarios where authentication is handled by the host.
/// </summary>
public class NoopAuthenticationProviderManager : IAuthenticationProviderManager
{
    public Task<string?> GetAuthenticationTokenAsync(string? tokenName, CancellationToken cancellationToken = default)
    {
        return Task.FromResult<string?>(null);
    }
}
