using Grove.Contents.Management;

namespace Grove.Contents.PublishLater.Models;

public class PublishLaterPart : ContentPart
{
    public DateTime? ScheduledPublishUtc { get; set; }
}
