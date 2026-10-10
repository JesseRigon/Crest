using Crest.ContentManagement;

namespace Crest.Lists.ViewModels;

public class ListPartSummaryAdminViewModel
{
    public ContentItem ContentItem { get; set; }

    public string[] ContainedContentTypes { get; set; }
}
