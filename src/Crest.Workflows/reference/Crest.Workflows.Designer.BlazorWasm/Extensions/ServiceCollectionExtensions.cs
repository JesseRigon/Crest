using Crest.Workflows.Studio.Core.BlazorWasm.Extensions;
using Crest.Workflows.Studio.Login.BlazorWasm.Extensions;
using Crest.Workflows.Studio.Workflows.Designer.Extensions;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Crest.Workflows.Designer.Extensions;

/// <summary>
/// Provides convenience methods for configuring the Crest.Workflows Designer for Blazor WebAssembly hosts.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Adds Crest.Workflows Designer services configured for Blazor WebAssembly hosting.
    /// </summary>
    public static IServiceCollection AddCrestWorkflowsDesigner(this IServiceCollection services, IConfiguration configuration)
    {
        return services.AddCrestWorkflowsDesignerCore(configuration, platformServices =>
        {
            platformServices.AddCore();
            platformServices.AddLoginModule();
        });
    }

    /// <summary>
    /// Registers the Crest.Workflows Designer custom elements as root components.
    /// </summary>
    public static RootComponentMappingCollection RegisterCrestWorkflowsDesignerComponents(this RootComponentMappingCollection rootComponents)
    {
        rootComponents.RegisterCustomCrestWorkflowsStudioElements();
        return rootComponents;
    }
}
