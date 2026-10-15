using Grove.ContentManagement.Metadata.Models;

namespace Grove.Contents.ViewModels;

public class ListContentTypesViewModel
{
    public IEnumerable<ContentTypeDefinition> Types { get; set; }
}
