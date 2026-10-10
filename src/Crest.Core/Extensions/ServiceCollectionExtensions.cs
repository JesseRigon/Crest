using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Crest.Environment.Extensions.Features;

namespace Crest.Environment.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddExtensionManagerHost(this IServiceCollection services)
    {
        services.AddSingleton<IExtensionManager, ExtensionManager>();
        services.AddSingleton<ITypeFeatureProvider, TypeFeatureProvider>();
        services.AddTransient<IFeaturesProvider, FeaturesProvider>();
        services.AddTransient<IExtensionDependencyStrategy, ExtensionDependencyStrategy>();
        services.AddTransient<IExtensionPriorityStrategy, ExtensionPriorityStrategy>();

        return services;
    }

    public static IServiceCollection AddExtensionManager(this IServiceCollection services)
    {
        services.TryAddTransient<IFeatureHash, FeatureHash>();

        return services;
    }
}
