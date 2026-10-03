using System.Text.Json.Nodes;

namespace Crest.Workflows.Studio.Workflows.UI.Contracts;

/// <summary>
/// Implement this interface to provide a diagram editor for a given workflow definition's root activity.
/// </summary>
public interface IDiagramDesignerProvider
{
    double Priority { get; }
    bool GetSupportsActivity(JsonObject activity);
    IDiagramDesigner GetEditor();
}