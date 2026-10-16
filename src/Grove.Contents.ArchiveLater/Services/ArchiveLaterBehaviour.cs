using Grove.Contents.Management;
using Grove.Contents.ArchiveLater.Models;

namespace Grove.Contents.ArchiveLater.Services;

/// <summary>What the scheduled archiveing's system flow does: a archiveed item's schedule is cleared, whoever archiveed it.</summary>
public sealed class ArchiveLaterBehaviour
{
    public Task ClearScheduleAsync(ContentItem item)
    {
        if (item.TryGet<ArchiveLaterFieldSet>(out var part) && part.ScheduledArchiveUtc.HasValue)
        {
            item.Alter<ArchiveLaterFieldSet>(schedule => schedule.ScheduledArchiveUtc = null);
        }

        return Task.CompletedTask;
    }
}
