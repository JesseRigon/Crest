using Crest.ContentManagement;

namespace Crest.PublishLater.Models;

public class PublishLaterPart : ContentPart
{
    public DateTime? ScheduledPublishUtc { get; set; }
}
