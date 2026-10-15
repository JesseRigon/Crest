using Grove.Workflows.Studio.Workflows.Designer.Components.ActivityWrappers.V2;
using Microsoft.AspNetCore.Components.Web;

namespace Grove.Workflows.Studio.Workflows.Designer.Extensions;

/// <summary>
/// Contains extension methods for <see cref="IJSComponentConfiguration"/>.
/// </summary>
public static class ComponentConfigurationExtensions
{
    /// <summary>
    /// Registers custom elements.
    /// </summary>
    public static IJSComponentConfiguration RegisterCustomGroveWorkflowsStudioElements(this IJSComponentConfiguration configuration, Type? activityComponentType = null)
    {
        activityComponentType ??= typeof(ActivityWrapper);
        configuration.RegisterForJavaScript(activityComponentType, "grove-workflows-activity-wrapper", "registerBlazorCustomElement");

        return configuration;
    }
}