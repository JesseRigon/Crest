using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Crest.BackgroundTasks;
using Crest.Contents.VersionPruning.Models;
using Crest.Contents.VersionPruning.Services;
using Crest.Modules;
using Crest.Settings;

namespace Crest.Contents.VersionPruning.Tasks;

[BackgroundTask(
    Schedule = "0 0 * * *",
    Title = "Content Version Pruning Background Task",
    Description = "Regularly deletes old non-latest, non-published content item versions.",
    Enable = false,
    LockTimeout = 3_000,
    LockExpiration = 30_000)]
public sealed class ContentVersionPruningBackgroundTask : IBackgroundTask
{
    public async Task DoWorkAsync(IServiceProvider serviceProvider, CancellationToken cancellationToken)
    {
        var siteService = serviceProvider.GetRequiredService<ISiteService>();

        var settings = await siteService.GetSettingsAsync<ContentVersionPruningSettings>();
        if (settings.Disabled)
        {
            return;
        }

        var logger = serviceProvider.GetRequiredService<ILogger<ContentVersionPruningBackgroundTask>>();

        try
        {
            var pruningService = serviceProvider.GetRequiredService<IContentVersionPruningService>();

            logger.LogDebug("Starting content version pruning.");

            var pruned = await pruningService.PruneVersionsAsync(settings, cancellationToken);

            if (logger.IsEnabled(LogLevel.Debug))
            {
                logger.LogDebug("Content version pruning completed. {PrunedCount} versions were deleted.", pruned);
            }
        }
        catch (Exception ex) when (!ex.IsFatal())
        {
            logger.LogError(ex, "Error while pruning content item versions.");
        }
    }
}
