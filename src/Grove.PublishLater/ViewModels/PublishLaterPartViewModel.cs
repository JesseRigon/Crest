using Microsoft.AspNetCore.Mvc.ModelBinding;
using Grove.ContentManagement;

namespace Grove.PublishLater.ViewModels;

public class PublishLaterPartViewModel
{
    [BindNever]
    public ContentItem ContentItem { get; set; }

    public DateTime? ScheduledPublishUtc { get; set; }

    public DateTime? ScheduledPublishLocalDateTime { get; set; }
}
