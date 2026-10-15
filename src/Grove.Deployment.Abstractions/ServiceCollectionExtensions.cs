using System.Text.Json.Serialization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Grove.Deployment;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddDeploymentTargetHandler<TImplementation>(this IServiceCollection services)
        where TImplementation : class, IDeploymentTargetHandler
    {
        services.AddScoped<IDeploymentTargetHandler, TImplementation>();

        return services;
    }

    /// <summary>
    /// Registers a deployment step and the source that processes it. A plan's steps are edited as
    /// the JSON the plan stores, so a step needs no editor.
    /// </summary>
    public static IServiceCollection AddDeployment<TSource, TStep>(this IServiceCollection services)
        where TSource : IDeploymentSource
        where TStep : DeploymentStep
    {
        services.AddDeploymentSource<TSource>()
            .AddDeploymentStep<TStep>();

        return services;
    }

    /// <summary>Registers a deployment step that another registered source processes.</summary>
    public static IServiceCollection AddDeploymentWithoutSource<TStep>(this IServiceCollection services)
        where TStep : DeploymentStep
        => services.AddDeploymentStep<TStep>();

    private static IServiceCollection AddDeploymentSource<TSource>(this IServiceCollection services)
        where TSource : IDeploymentSource
    {
        services.TryAddEnumerable(ServiceDescriptor.Transient(typeof(IDeploymentSource), typeof(TSource)));

        return services;
    }

    private static IServiceCollection AddDeploymentStep<TStep>(this IServiceCollection services)
        where TStep : DeploymentStep
    {
        services.TryAddEnumerable(ServiceDescriptor.Transient<IDeploymentStepFactory, DeploymentStepFactory<TStep>>());

        // TStep instances are part for DeploymentPlan objects that are serialized to JSON
        services.AddJsonDerivedTypeInfo<TStep, DeploymentStep>();

        return services;
    }
}
