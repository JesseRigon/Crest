using System.Text.Json.Nodes;
using Crest.Workflows.Api.Client.Resources.WorkflowDefinitions.Models;
using Crest.Workflows.Studio.Workflows.UI.Contracts;

namespace Crest.Workflows.Studio.Workflows.DiagramDesigners.Fallback;

/// <summary>
/// Provides fallback designer services.
/// </summary>
public class FallbackDesignerProvider : IDiagramDesignerProvider
{
    /// <summary>
    /// Provides the priority.
    /// </summary>
    public double Priority => -1000;
    /// <summary>
    /// Provides the get supports activity.
    /// </summary>
    public bool GetSupportsActivity(JsonObject activity) => true;

    /// <summary>
    /// Provides the get editor.
    /// </summary>
    public IDiagramDesigner GetEditor() => new FallbackDiagramDesigner();
}