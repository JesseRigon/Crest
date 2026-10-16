using YesSql.Indexes;

namespace Grove.Contents.ArchiveLater.Indexes;

public class ArchiveLaterIndex : MapIndex
{
    public string ContentItemId { get; set; }

    public DateTime? ScheduledArchiveDateTimeUtc { get; set; }

    public bool Published { get; set; }

    public bool Latest { get; set; }
}
