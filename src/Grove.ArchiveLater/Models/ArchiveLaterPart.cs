using Grove.ContentManagement;

namespace Grove.ArchiveLater.Models;

public class ArchiveLaterPart : ContentPart
{
    public DateTime? ScheduledArchiveUtc { get; set; }
}
