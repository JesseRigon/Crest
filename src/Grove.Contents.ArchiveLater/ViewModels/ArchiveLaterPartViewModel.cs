using Microsoft.AspNetCore.Mvc.ModelBinding;
using Grove.Contents.Management;

namespace Grove.Contents.ArchiveLater.ViewModels;

public class ArchiveLaterPartViewModel
{
    [BindNever]
    public ContentItem ContentItem { get; set; }

    public DateTime? ScheduledArchiveUtc { get; set; }

    public DateTime? ScheduledArchiveLocalDateTime { get; set; }
}
