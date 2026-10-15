using Grove.Contents.Management;

namespace Grove.Contents.ArchiveLater.Models;

public class ArchiveLaterPart : ContentPart
{
    public DateTime? ScheduledArchiveUtc { get; set; }
}
