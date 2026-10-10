using Crest.Workflows.Studio.Core.BlazorServer.Extensions;
using Crest.Workflows.Studio.Login.BlazorServer.Extensions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Crest.Workflows.Designer.Extensions;

/// <summary>
/// Provides convenience methods for configuring the Crest.Workflows Designer for Blazor Server hosts.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Adds Crest.Workflows Designer services configured for Blazor Server hosting.
    /// </summary>
    public static IServiceCollection AddCrestWorkflowsDesigner(this IServiceCollection services, IConfiguration configuration)
    {
        return services.AddCrestWorkflowsDesignerCore(configuration, platformServices =>
        {
            platformServices.AddCore();
            platformServices.AddLoginModule();
        });
    }
}
