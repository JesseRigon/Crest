using Microsoft.Extensions.DependencyInjection;
using Grove.Deployment.Services;
using Grove.Deployment.Services;

namespace Grove.Deployment;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddDeploymentServices(this IServiceCollection services)
    {
        services.AddScoped<IDeploymentManager, DeploymentManager>();

        return services;
    }
}
