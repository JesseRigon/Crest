using Grove.ContentManagement;
using Grove.ContentManagement.Display.Models;
using Grove.ContentManagement.Metadata.Models;
using Grove.Lists.Models;

namespace Grove.Lists.ViewModels;

public class ListPartViewModel
{
    public ListPartFilterViewModel ListPartFilterViewModel { get; set; }
    public ListPart ListPart { get; set; }
    public IEnumerable<ContentItem> ContentItems { get; set; }
    public IEnumerable<ContentTypeDefinition> ContainedContentTypeDefinitions { get; set; }
    public BuildPartDisplayContext Context { get; set; }
    public dynamic Pager { get; set; }
    public bool EnableOrdering { get; set; }
}
