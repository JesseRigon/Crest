using Grove.Contents.Management;
using Grove.Contents.PublishLater.Models;

namespace Grove.Contents.PublishLater.Services;

/// <summary>What the scheduled publishing's system flow does: a published item's schedule is cleared, whoever published it.</summary>
public sealed class PublishLaterBehaviour
{
    public Task ClearScheduleAsync(ContentItem item)
    {
        if (item.TryGet<PublishLaterFieldSet>(out var part) && part.ScheduledPublishUtc.HasValue)
        {
            item.Alter<PublishLaterFieldSet>(schedule => schedule.ScheduledPublishUtc = null);
        }

        return Task.CompletedTask;
    }
}
