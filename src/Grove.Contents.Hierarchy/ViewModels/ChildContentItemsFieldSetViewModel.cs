using Grove.Contents.Management;
using Grove.Contents.Management.Display.Models;
using Grove.Contents.Management.Metadata.Models;
using Grove.Contents.Hierarchy.Models;

namespace Grove.Contents.Hierarchy.ViewModels;

public class ChildContentItemsFieldSetViewModel
{
    public ChildContentItemsFieldSet ChildContentItemsFieldSet { get; set; }
    public IEnumerable<ContentItem> ContentItems { get; set; }
    public IEnumerable<ContentTypeDefinition> ContainedContentTypeDefinitions { get; set; }
    public BuildFieldSetDisplayContext Context { get; set; }
    public dynamic Pager { get; set; }
    public bool EnableOrdering { get; set; }
}
