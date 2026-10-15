using Grove.ContentManagement;

namespace Grove.PublishLater.Models;

public class PublishLaterPart : ContentPart
{
    public DateTime? ScheduledPublishUtc { get; set; }
}
