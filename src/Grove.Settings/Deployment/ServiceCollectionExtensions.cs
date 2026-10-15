using System.Text.Json.Serialization;
using Microsoft.Extensions.DependencyInjection;
using Grove.Deployment;

namespace Grove.Settings.Deployment;

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers a <see cref="SiteSettingsPropertyDeploymentSource{TModel}"/> step.
    /// </summary>
    public static void AddSiteSettingsPropertyDeploymentStep<TModel>(this IServiceCollection services) where TModel : class, new()
    {
        services.AddTransient<IDeploymentSource, SiteSettingsPropertyDeploymentSource<TModel>>();
        services.AddSingleton<IDeploymentStepFactory>(new SiteSettingsPropertyDeploymentStepFactory<TModel>());
        services.AddJsonDerivedTypeInfo<SiteSettingsPropertyDeploymentStep<TModel>, DeploymentStep>();
    }
}
