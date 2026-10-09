using Crest.ContentManagement;

namespace Crest.ArchiveLater.Models;

public class ArchiveLaterPart : ContentPart
{
    public DateTime? ScheduledArchiveUtc { get; set; }
}
