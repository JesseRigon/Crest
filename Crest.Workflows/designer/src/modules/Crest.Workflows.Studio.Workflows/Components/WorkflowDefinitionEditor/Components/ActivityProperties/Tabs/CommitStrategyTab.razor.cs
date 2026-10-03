using System.Text.Json.Nodes;
using Crest.Workflows.Api.Client.Extensions;
using Crest.Workflows.Api.Client.Resources.CommitStrategies.Models;
using Crest.Workflows.Studio.Workflows.Domain.Contracts;
using Crest.Workflows.Studio.Workflows.UI.Contracts;
using Microsoft.AspNetCore.Components;

namespace Crest.Workflows.Studio.Workflows.Components.WorkflowDefinitionEditor.Components.ActivityProperties.Tabs;

/// <summary>
/// The Task tab for an activity.
/// </summary>
public partial class CommitStrategyTab
{
    /// The activity.
    [Parameter] public JsonObject? Activity { get; set; }
    
    /// An event raised when the activity is updated.
    [Parameter] public Func<JsonObject, Task>? OnActivityUpdated { get; set; }
    [CascadingParameter] private IWorkspace? Workspace { get; set; }
    [Inject] private ICommitStrategiesProvider CommitStrategiesProvider { get; set; } = null!;
    private bool IsReadOnly => Workspace?.IsReadOnly == true;
    private ICollection<CommitStrategyDescriptor?> _commitStrategies = [];
    private CommitStrategyDescriptor? _selectedCommitStrategy;

    /// <inheritdoc />
    protected override async Task OnInitializedAsync()
    {
        var commitStrategies = (await CommitStrategiesProvider.GetActivityCommitStrategiesAsync()).ToList();
        commitStrategies.Insert(0, null!);
        _commitStrategies = commitStrategies!;
        _selectedCommitStrategy = _commitStrategies.FirstOrDefault(x => x?.Name == Activity!.GetCommitStrategy());
        await base.OnInitializedAsync();
    }
    
    private async Task RaiseActivityUpdated()
    {
        if (OnActivityUpdated != null)
            await OnActivityUpdated(Activity!);
    }

    private async Task OnCommitStrategySelectionChanged(CommitStrategyDescriptor? value)
    {
        _selectedCommitStrategy = value;
        Activity!.SetCommitStrategy(value?.Name);
        await RaiseActivityUpdated();
    }
}