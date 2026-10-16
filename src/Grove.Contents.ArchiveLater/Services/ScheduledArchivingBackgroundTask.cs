using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Grove.Contents.ArchiveLater.Indexes;
using Grove.Contents.ArchiveLater.Models;
using Grove.BackgroundTasks;
using Grove.Contents.Management;
using Grove.Entities;
using Grove.Modules;
using YesSql;

namespace Grove.Contents.ArchiveLater.Services;

[BackgroundTask(
    Title = "Content Items Archiver",
    Schedule = "* * * * *",
    Description = "Archives content items when their scheduled archive date time arrives.")]
public sealed class ScheduledArchivingBackgroundTask : IBackgroundTask
{
    private readonly ILogger _logger;
    private readonly IClock _clock;

    public ScheduledArchivingBackgroundTask(ILogger<ScheduledArchivingBackgroundTask> logger, IClock clock)
    {
        _logger = logger;
        _clock = clock;
    }

    public async Task DoWorkAsync(IServiceProvider serviceProvider, CancellationToken cancellationToken)
    {
        var itemsToArchive = await serviceProvider
            .GetRequiredService<ISession>()
            .QueryIndex<ArchiveLaterIndex>(index => index.Latest && index.Published && index.ScheduledArchiveDateTimeUtc < _clock.UtcNow)
            .ListAsync(cancellationToken);

        if (!itemsToArchive.Any())
        {
            return;
        }

        var contentManager = serviceProvider.GetRequiredService<IContentManager>();

        foreach (var item in itemsToArchive)
        {
            var contentItem = await contentManager.GetAsync(item.ContentItemId);

            // The schedule is cleared by the field set's system flow when the item is unpublished.

            if (_logger.IsEnabled(LogLevel.Debug))
            {
                _logger.LogDebug("Archiving scheduled content item {ContentItemId}.", contentItem.ContentItemId);
            }

            await contentManager.UnpublishAsync(contentItem);
        }
    }
}
