using Microsoft.AspNetCore.Mvc.ModelBinding;
using Crest.ContentManagement;

namespace Crest.PublishLater.ViewModels;

public class PublishLaterPartViewModel
{
    [BindNever]
    public ContentItem ContentItem { get; set; }

    public DateTime? ScheduledPublishUtc { get; set; }

    public DateTime? ScheduledPublishLocalDateTime { get; set; }
}
