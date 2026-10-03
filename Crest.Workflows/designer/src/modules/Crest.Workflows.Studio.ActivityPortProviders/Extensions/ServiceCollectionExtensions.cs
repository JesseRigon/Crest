using Crest.Workflows.Studio.ActivityPortProviders.Providers;
using Crest.Workflows.Studio.Workflows.Extensions;
using Microsoft.Extensions.DependencyInjection;

namespace Crest.Workflows.Studio.ActivityPortProviders.Extensions;

/// <summary>
/// Contains extension methods for the <see cref="IServiceCollection"/> interface.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Adds default activity port providers.
    /// </summary>
    public static IServiceCollection AddDefaultActivityPortProviders(this IServiceCollection services)
    {
        services.AddActivityPortProvider<DynamicOutcomesPortProvider>();
        services.AddActivityPortProvider<SwitchPortProvider>();
        services.AddActivityPortProvider<FlowSwitchPortProvider>();
        services.AddActivityPortProvider<HttpEndpointPortProvider>();
        services.AddActivityPortProvider<SendHttpRequestPortProvider>();
        services.AddActivityPortProvider<FlowHttpRequestPortProvider>();
        
        return services;
    }
}