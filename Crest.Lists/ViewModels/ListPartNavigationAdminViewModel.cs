using Crest.ContentManagement;
using Crest.ContentManagement.Metadata.Models;

namespace Crest.Lists.ViewModels;

public class ListPartNavigationAdminViewModel
{
    public ContentItem Container { get; set; }

    public ContentTypeDefinition ContainerContentTypeDefinition { get; set; }

    public bool EnableOrdering { get; set; }

    public ContentTypeDefinition[] ContainedContentTypeDefinitions { get; set; } = [];
}
