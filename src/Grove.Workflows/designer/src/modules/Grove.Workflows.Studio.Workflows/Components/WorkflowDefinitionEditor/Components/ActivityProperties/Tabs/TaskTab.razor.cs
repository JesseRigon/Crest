using System.Text.Json.Nodes;
using Grove.Workflows.Api.Client.Extensions;
using Grove.Workflows.Api.Client.Resources.ActivityDescriptors.Models;
using Grove.Workflows.Studio.Workflows.UI.Contracts;
using Microsoft.AspNetCore.Components;

namespace Grove.Workflows.Studio.Workflows.Components.WorkflowDefinitionEditor.Components.ActivityProperties.Tabs;

/// <summary>
/// The Task tab for an activity.
/// </summary>
public partial class TaskTab
{
    /// The activity.
    [Parameter] public JsonObject? Activity { get; set; }
    
    /// The activity descriptor.
    [Parameter]
    /// <summary>
    /// Gets or sets the activity descriptor.
    /// </summary>
    public ActivityDescriptor ActivityDescriptor { get; set; } = default!;
    
    /// An event raised when the activity is updated.
    [Parameter] public Func<JsonObject, Task>? OnActivityUpdated { get; set; }
    [CascadingParameter] private IWorkspace? Workspace { get; set; }
    private bool IsReadOnly => Workspace?.IsReadOnly == true;

    /// <inheritdoc />
    protected override void OnParametersSet()
    {
    }
    
    private async Task RaiseActivityUpdated()
    {
        if (OnActivityUpdated != null)
            await OnActivityUpdated(Activity!);
    }

    private async Task OnRunAsynchronouslyChanged(bool? value)
    {
        Activity!.SetRunAsynchronously(value == true);
        await RaiseActivityUpdated();
    }
}