using Crest.Workflows.Api.Client.Resources.WorkflowDefinitions.Models;
using Crest.Workflows.Studio.Workflows.UI.Contracts;
using Microsoft.AspNetCore.Components;

namespace Crest.Workflows.Studio.Workflows.Components.WorkflowDefinitionEditor.Components.WorkflowProperties.Tabs.InputOutput.Components;

/// <summary>
/// Represents the input output tab.
/// </summary>
public partial class InputOutputTab
{
    [Parameter] public WorkflowDefinition WorkflowDefinition { get; set; } = default!;
    [Parameter] public EventCallback WorkflowDefinitionUpdated { get; set; }
    [CascadingParameter] public IWorkspace? Workspace { get; set; }
    
    private bool IsReadOnly => Workspace?.IsReadOnly ?? true;
    
}