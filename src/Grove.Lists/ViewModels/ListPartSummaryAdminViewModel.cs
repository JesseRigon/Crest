using Grove.ContentManagement;

namespace Grove.Lists.ViewModels;

public class ListPartSummaryAdminViewModel
{
    public ContentItem ContentItem { get; set; }

    public string[] ContainedContentTypes { get; set; }
}
