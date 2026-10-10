using Crest.Workflows.Studio.Contracts;
using Crest.Workflows.Studio.Workflows.Designer.Contracts;
using Crest.Workflows.Studio.Workflows.Designer.Interop;
using Crest.Workflows.Studio.Workflows.Designer.Services;
using Microsoft.Extensions.DependencyInjection;

namespace Crest.Workflows.Studio.Workflows.Designer.Extensions;

/// <summary>
/// Provides extension methods for service collection.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Adds the workflows designer.
    /// </summary>
    /// <param name="services">The service collection to configure.</param>
    /// <returns>The result of the operation.</returns>
    public static IServiceCollection AddWorkflowsDesigner(this IServiceCollection services)
    {
        return services
            .AddScoped<IFeature, Feature>()
            .AddScoped<IMapperFactory, MapperFactory>()
            .AddScoped<DesignerJsInterop>();
    }
}