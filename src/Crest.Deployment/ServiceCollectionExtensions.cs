using Microsoft.Extensions.DependencyInjection;
using Crest.Deployment.Services;
using Crest.Deployment.Services;

namespace Crest.Deployment;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddDeploymentServices(this IServiceCollection services)
    {
        services.AddScoped<IDeploymentManager, DeploymentManager>();

        return services;
    }
}
