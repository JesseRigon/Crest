using Microsoft.Extensions.DependencyInjection;
using Crest.Deployment.Core.Services;
using Crest.Deployment.Services;

namespace Crest.Deployment.Core;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddDeploymentServices(this IServiceCollection services)
    {
        services.AddScoped<IDeploymentManager, DeploymentManager>();

        return services;
    }
}
