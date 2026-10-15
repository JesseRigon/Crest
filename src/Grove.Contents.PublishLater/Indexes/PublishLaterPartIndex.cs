using YesSql.Indexes;

namespace Grove.Contents.PublishLater.Indexes;

public class PublishLaterPartIndex : MapIndex
{
    public string ContentItemId { get; set; }

    public DateTime? ScheduledPublishDateTimeUtc { get; set; }

    public bool Published { get; set; }

    public bool Latest { get; set; }
}
