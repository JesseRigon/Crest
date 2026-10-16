using Grove.Workflows.Common;
using Grove.Workflows.Common.RecurringTasks;
using Grove.Workflows.Management;
using Grove.Workflows.Management.Filters;
using Grove.Workflows.Runtime.Options;
using JetBrains.Annotations;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Grove.Workflows.Runtime.Tasks;

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

        // One sweep restarts each instance at most once: a restart only queues the instance, so a store
        // that returned it again would otherwise queue it again, without bound.
        var restarted = new HashSet<string>(StringComparer.Ordinal);

        logger.LogInformation("Restarting interrupted workflows.");
        await foreach (var workflowInstance in workflowInstances)
        {
            if (!restarted.Add(workflowInstance.Id))
                break;

            await workflowRestarter.RestartWorkflowAsync(workflowInstance.Id, cancellationToken: cancellationToken);
        }
        logger.LogInformation("Finished restarting interrupted workflows ({Count} restarted).", restarted.Count);
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