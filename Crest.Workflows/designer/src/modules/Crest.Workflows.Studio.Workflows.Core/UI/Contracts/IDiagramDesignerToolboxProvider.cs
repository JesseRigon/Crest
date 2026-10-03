using Microsoft.AspNetCore.Components;

namespace Crest.Workflows.Studio.Workflows.UI.Contracts;

/// <summary>
/// Implement this interface to provide toolbox items for the diagram editor.
/// </summary>
public interface IDiagramDesignerToolboxProvider : IDiagramDesigner
{
    IEnumerable<RenderFragment> GetToolboxItems(bool isReadOnly);
}