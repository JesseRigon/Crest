using Crest.BackgroundTasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Crest.Sharing.Services;

/// <summary>
/// The per-tenant access index job (docs/sharing.md, requirements 79-80): every minute it
/// recalculates the objects whose next time boundary has come (only those: the registry is
/// indexed by time, nothing is scanned) and runs the subtree recalculations a change that
/// only added access left to the background. Access never waits for it: index rows carry their
/// windows and every read compares them with now.
/// </summary>
[BackgroundTask(
    Title = "Sharing access index",
    Schedule = "* * * * *",
    Description = "Recalculates shared items whose access starts or ends now, and pending subtree recalculations.")]
public sealed class ShareAccessBackgroundTask(ILogger<ShareAccessBackgroundTask> logger) : IBackgroundTask
{
    /// <summary>Due objects per run; the rest are due on the next.</summary>
    public const int DueBatch = 500;

    /// <summary>Queued subtrees per run.</summary>
    public const int QueuedBatch = 5;

    public async Task DoWorkAsync(IServiceProvider serviceProvider, CancellationToken cancellationToken)
    {
        var indexer = serviceProvider.GetRequiredService<ShareAccessIndexer>();
        try
        {
            await indexer.RecalculateDueAsync(DueBatch, cancellationToken);
            await indexer.RecalculateQueuedAsync(QueuedBatch, cancellationToken);
            await serviceProvider.GetRequiredService<IShareAccessStore>().CommitAsync();
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            logger.LogError(exception, "Recalculating the access index failed; it is retried on the next run.");
        }
    }
}
