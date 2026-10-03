using Crest.Workflows.Studio.Workflows.Designer.Components.ActivityWrappers.V2;
using Microsoft.AspNetCore.Components.Web;

namespace Crest.Workflows.Studio.Workflows.Designer.Extensions;

/// <summary>
/// Contains extension methods for <see cref="IJSComponentConfiguration"/>.
/// </summary>
public static class ComponentConfigurationExtensions
{
    /// <summary>
    /// Registers custom elements.
    /// </summary>
    public static IJSComponentConfiguration RegisterCustomCrestWorkflowsStudioElements(this IJSComponentConfiguration configuration, Type? activityComponentType = null)
    {
        activityComponentType ??= typeof(ActivityWrapper);
        configuration.RegisterForJavaScript(activityComponentType, "crest-workflows-activity-wrapper", "registerBlazorCustomElement");

        return configuration;
    }
}