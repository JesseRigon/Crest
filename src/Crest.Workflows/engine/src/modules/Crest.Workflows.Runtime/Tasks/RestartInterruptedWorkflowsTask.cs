using Crest.Workflows.Common;
using Crest.Workflows.Common.RecurringTasks;
using Crest.Workflows.Management;
using Crest.Workflows.Management.Filters;
using Crest.Workflows.Runtime.Options;
using JetBrains.Annotations;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Crest.Workflows.Runtime.Tasks;

[SingleNodeTask]
[UsedImplicitly]
public class RestartInterruptedWorkflowsTask(
    IWorkflowInstanceStore workflowInstanceStore, 
    IWorkflowRestarter workflowRestarter, 
    IOptions<RuntimeOptions> options, 
    ISystemClock systemClock,
    ILogger<RestartInterruptedWorkflowsTask> logger) : RecurringTask
{
    /// <inheritdoc />
    public override async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        var workflowInstanceFilter = CreateWorkflowInstanceFilter();
        var batchSize = options.Value.RestartInterruptedWorkflowsBatchSize;
        var workflowInstances = workflowInstanceStore.EnumerateSummariesAsync(workflowInstanceFilter, batchSize, cancellationToken);

        logger.LogInformation("Restarting interrupted workflows.");
        await foreach (var workflowInstance in workflowInstances)
        {
            await workflowRestarter.RestartWorkflowAsync(workflowInstance.Id, cancellationToken: cancellationToken);
        }
        logger.LogInformation("Finished restarting interrupted workflows.");
    }

    private WorkflowInstanceFilter CreateWorkflowInstanceFilter()
    {
        var livenessThreshold = options.Value.InactivityThreshold;
        var now = systemClock.UtcNow;
        var cutoffTimestamp = now - livenessThreshold;
        return new()
        {
            IsExecuting = true,
            BeforeLastUpdated = cutoffTimestamp
        };
    }
}