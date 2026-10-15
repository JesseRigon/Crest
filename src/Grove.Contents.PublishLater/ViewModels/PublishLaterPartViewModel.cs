using Microsoft.AspNetCore.Mvc.ModelBinding;
using Grove.Contents.Management;

namespace Grove.Contents.PublishLater.ViewModels;

public class PublishLaterPartViewModel
{
    [BindNever]
    public ContentItem ContentItem { get; set; }

    public DateTime? ScheduledPublishUtc { get; set; }

    public DateTime? ScheduledPublishLocalDateTime { get; set; }
}
