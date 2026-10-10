using Crest.ContentManagement;
using Crest.ContentManagement.Display.Models;
using Crest.ContentManagement.Metadata.Models;
using Crest.Lists.Models;

namespace Crest.Lists.ViewModels;

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
