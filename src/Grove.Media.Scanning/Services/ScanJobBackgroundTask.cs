using Grove.BackgroundTasks;
using Grove.Media.Files;
using Grove.Modules;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Grove.Media.Scanning.Services;

/// <summary>
/// Picks up scan jobs that are due: ones the deferred run after upload never reached, failures
/// waiting for their retry, offsite scanners that stayed pending too long, and verdicts the
/// handler has not accepted yet.
/// </summary>
[BackgroundTask(
    Title = "Upload Scanning",
    Schedule = "* * * * *",
    Description = "Retries quarantined uploads whose scanning failed, timed out or was not started.")]
public sealed class ScanJobBackgroundTask(ILogger<ScanJobBackgroundTask> logger) : IBackgroundTask
{
    private const int BatchSize = 50;

    public async Task DoWorkAsync(IServiceProvider serviceProvider, CancellationToken cancellationToken)
    {
        var store = serviceProvider.GetRequiredService<IScanJobStore>();
        var clock = serviceProvider.GetRequiredService<IClock>();
        var due = await store.ListDueAsync(clock.UtcNow, BatchSize, cancellationToken);
        if (due.Count == 0)
        {
            return;
        }

        if (serviceProvider.GetService<IMediaStorageProvider>() is null)
        {
            logger.LogWarning("{Count} uploads wait in quarantine but no media storage provider is registered.", due.Count);
            return;
        }

        var coordinator = serviceProvider.GetRequiredService<ScanCoordinator>();

        foreach (var scanId in due)
        {
            try
            {
                await coordinator.RunAsync(scanId, cancellationToken);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                logger.LogError(exception, "Scanning '{ScanId}' failed; it is retried on the next run.", scanId);
            }
        }
    }
}
