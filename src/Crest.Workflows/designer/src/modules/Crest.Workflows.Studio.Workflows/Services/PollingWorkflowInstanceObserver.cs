using System.Text.Json.Nodes;
using Crest.Workflows.Api.Client.Extensions;
using Crest.Workflows.Api.Client.RealTime.Messages;
using Crest.Workflows.Api.Client.Resources.ActivityExecutions.Contracts;
using Crest.Workflows.Api.Client.Resources.ActivityExecutions.Models;
using Crest.Workflows.Api.Client.Resources.ActivityExecutions.Requests;
using Crest.Workflows.Api.Client.Resources.WorkflowInstances.Contracts;
using Crest.Workflows.Api.Client.Resources.WorkflowInstances.Enums;
using Crest.Workflows.Studio.Contracts;
using Crest.Workflows.Studio.Workflows.Contracts;
using Crest.Workflows.Studio.Workflows.Models;

namespace Crest.Workflows.Studio.Workflows.Services;

/// Observes a workflow instance by periodically polling for updates and raising corresponding events.
public class PollingWorkflowInstanceObserver : IWorkflowInstanceObserver
{
    private readonly IWorkflowInstancesApi _workflowInstancesApi;
    private readonly IActivityExecutionsApi _activityExecutionsApi;
    private readonly string _workflowInstanceId;
    private readonly JsonObject? _containerActivity;
    private readonly TimeSpan _pollingInterval = TimeSpan.FromSeconds(5);
    private DateTimeOffset _lastUpdateAt = DateTimeOffset.MinValue;
    private bool _checkForUpdates = true;

    /// Observes a workflow instance by periodically polling for updates and raising corresponding events.
    public PollingWorkflowInstanceObserver(
        WorkflowInstanceObserverContext context,
        IWorkflowInstancesApi workflowInstancesApi,
        IActivityExecutionsApi activityExecutionsApi)
    {
        _workflowInstancesApi = workflowInstancesApi;
        _activityExecutionsApi = activityExecutionsApi;
        _workflowInstanceId = context.WorkflowInstanceId;
        _containerActivity = context.ContainerActivity;

        _ = Task.Run(GetRecentExecutionRecordsAsync);
    }

    /// <inheritdoc />
    public event Func<WorkflowExecutionLogUpdatedMessage, Task> WorkflowJournalUpdated = default!;

    /// <inheritdoc />
    public event Func<ActivityExecutionLogUpdatedMessage, Task> ActivityExecutionLogUpdated = default!;

    /// <inheritdoc />
    public event Func<WorkflowInstanceUpdatedMessage, Task> WorkflowInstanceUpdated = default!;

    private async Task GetRecentExecutionRecordsAsync()
    {
        while (_checkForUpdates)
        {
            try
            {
                var response = await _workflowInstancesApi.GetExecutionStateAsync(_workflowInstanceId);
                var lastUpdateAt = response.UpdatedAt;

                if (_lastUpdateAt < lastUpdateAt)
                {
                    _lastUpdateAt = lastUpdateAt;
                    if (WorkflowJournalUpdated != null!) await WorkflowJournalUpdated(new());
                    if (ActivityExecutionLogUpdated != null && _containerActivity != null)
                    {
                        var activityExecutionStats = await GetActivityExecutionStatsAsync(_containerActivity);
                        await ActivityExecutionLogUpdated(new(activityExecutionStats));
                    }

                    if (WorkflowInstanceUpdated != null!) await WorkflowInstanceUpdated(new(_workflowInstanceId));
                }

                if (response.Status == WorkflowStatus.Finished)
                    _checkForUpdates = false;
            }
            catch (ObjectDisposedException)
            {
                _checkForUpdates = false;
                break;
            }

            await Task.Delay(_pollingInterval);
        }
    }

    private async Task<ICollection<ActivityExecutionStats>> GetActivityExecutionStatsAsync(JsonObject container)
    {
        var activityNodeIds = container.GetActivities().Select(x => x.GetNodeId()).ToList(); 
        var request = new GetActivityExecutionReportRequest
        {
            WorkflowInstanceId = _workflowInstanceId, 
            ActivityNodeIds = activityNodeIds,
        };
        var report = await _activityExecutionsApi.GetReportAsync(request);
        return report.Stats;
    }

    /// <inheritdoc />
    public ValueTask DisposeAsync()
    {
        _checkForUpdates = false;
        return ValueTask.CompletedTask;
    }
}