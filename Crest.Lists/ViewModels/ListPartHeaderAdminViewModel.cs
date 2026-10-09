using Crest.ContentManagement;
using Crest.ContentManagement.Metadata.Models;

namespace Crest.Lists.ViewModels;

public class ListPartHeaderAdminViewModel
{
    public ContentItem ContainerContentItem { get; set; }

    public ContentTypeDefinition[] ContainedContentTypeDefinitions { get; set; } = [];

    public bool EnableOrdering { get; set; }
}
