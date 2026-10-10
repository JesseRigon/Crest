using Crest.Workflows.Api.Client.Resources.Resilience.Models;
using Crest.Workflows.Studio.Models;
using Microsoft.AspNetCore.Components;
using MudBlazor;

namespace Crest.Workflows.Studio.Workflows.Components.WorkflowInstanceViewer.Components;

/// Displays the details of an activity.
public partial class RetriesTab
{
    /// The activity execution record summaries.
    [Parameter] public ICollection<RetryAttemptRecord> Retries { get; set; } = new List<RetryAttemptRecord>();

    private RetryAttemptRecord? SelectedItem { get; set; }
    private DataPanelModel SelectedRetryAttemptData { get; set; } = new();


    private void CreateSelectedItemDataModels(RetryAttemptRecord? record)
    {
        if (record == null)
        {
            SelectedRetryAttemptData = new();
            return;
        }

        SelectedRetryAttemptData = new();

        foreach (var detail in record.Details)
        {
            SelectedRetryAttemptData.Add(detail.Key, detail.Value);
        }
    }

    private void OnRetryAttemptClicked(TableRowClickEventArgs<RetryAttemptRecord> arg)
    {
        SelectedItem = arg.Item;
        CreateSelectedItemDataModels(arg.Item);
    }
}