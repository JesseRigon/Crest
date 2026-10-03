using Crest.Workflows.Mediator.Contracts;
using Crest.Workflows.Runtime.Entities;
using Crest.Workflows.Runtime.Filters;
using Crest.Workflows.Runtime.Notifications;

namespace Crest.Workflows.Runtime;

/// <inheritdoc />
public class DefaultActivityExecutionManager : IActivityExecutionManager
{
    private readonly IActivityExecutionStore _store;
    private readonly INotificationSender _notificationSender;

    /// <summary>
    /// Initializes a new instance of the <see cref="DefaultActivityExecutionManager"/> class.
    /// </summary>
    public DefaultActivityExecutionManager(IActivityExecutionStore store, INotificationSender notificationSender)
    {
        _store = store;
        _notificationSender = notificationSender;
    }

    /// <inheritdoc />
    public async Task<long> DeleteManyAsync(ActivityExecutionRecordFilter filter, CancellationToken cancellationToken = default)
    {
        var records = (await _store.FindManyAsync(filter, cancellationToken)).ToList();

        foreach (var record in records)
        {
            await _store.DeleteManyAsync(filter, cancellationToken);
            await _notificationSender.SendAsync(new ActivityExecutionRecordDeleted(record), cancellationToken);
        }

        return records.Count;
    }

    /// <inheritdoc />
    public async Task SaveAsync(ActivityExecutionRecord record, CancellationToken cancellationToken = default)
    {
        await _store.SaveAsync(record, cancellationToken);
        await _notificationSender.SendAsync(new ActivityExecutionRecordUpdated(record), cancellationToken);
    }
}