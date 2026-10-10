using Crest.Workflows.Extensions;
using Crest.Workflows.Mediator.Contracts;
using Crest.Workflows.Runtime.Entities;
using Crest.Workflows.Runtime.Notifications;

namespace Crest.Workflows.Runtime;

/// <summary>
/// This implementation saves <see cref="ActivityExecutionRecord"/> directly through the store.
/// </summary>
public class StoreActivityExecutionLogSink(
    IActivityExecutionStore activityExecutionStore,
    INotificationSender notificationSender)
    : ILogRecordSink<ActivityExecutionRecord>
{
    /// <inheritdoc />
    public async Task PersistExecutionLogsAsync(WorkflowExecutionContext context, CancellationToken cancellationToken = default)
    {
        // Select tainted activity execution contexts to avoid saving untainted ones multiple times.
        var activityExecutionContexts = context.ActivityExecutionContexts.Where(x => x.IsDirty).ToList();

        if (activityExecutionContexts.Count == 0)
            return;

        var records = await Task.WhenAll(activityExecutionContexts.Select(x => x.GetOrMapCapturedActivityExecutionRecordAsync()));
        await activityExecutionStore.SaveManyAsync(records, cancellationToken);

        // Untaint activity execution contexts.
        foreach (var activityExecutionContext in activityExecutionContexts)
            activityExecutionContext.ClearTaint();

        await notificationSender.SendAsync(new ActivityExecutionLogUpdated(context, records), cancellationToken);
    }
}