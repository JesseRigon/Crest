using Microsoft.AspNetCore.Components.WebAssembly.Hosting;

namespace Crest.Workflows.Designer.Extensions;

/// <summary>
/// Extension methods for <see cref="WebAssemblyHostBuilder"/>.
/// </summary>
public static class WebAssemblyHostBuilderExtensions
{
    /// <summary>
    /// Configures the Crest.Workflows Designer by registering root components and adding all required services.
    /// </summary>
    /// <param name="builder">The WebAssembly host builder.</param>
    /// <returns>The WebAssembly host builder for chaining.</returns>
    public static WebAssemblyHostBuilder AddCrestWorkflowsDesigner(this WebAssemblyHostBuilder builder)
    {
        builder.RootComponents.RegisterCrestWorkflowsDesignerComponents();
        builder.Services.AddCrestWorkflowsDesigner(builder.Configuration);

        return builder;
    }
}
