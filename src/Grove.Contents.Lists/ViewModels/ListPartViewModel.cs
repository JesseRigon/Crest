using Grove.Contents.Management;
using Grove.Contents.Management.Display.Models;
using Grove.Contents.Management.Metadata.Models;
using Grove.Contents.Lists.Models;

namespace Grove.Contents.Lists.ViewModels;

public class ListPartViewModel
{
    public ListPart ListPart { get; set; }
    public IEnumerable<ContentItem> ContentItems { get; set; }
    public IEnumerable<ContentTypeDefinition> ContainedContentTypeDefinitions { get; set; }
    public BuildPartDisplayContext Context { get; set; }
    public dynamic Pager { get; set; }
    public bool EnableOrdering { get; set; }
}
