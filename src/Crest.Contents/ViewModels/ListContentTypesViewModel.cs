using Crest.ContentManagement.Metadata.Models;

namespace Crest.Contents.ViewModels;

public class ListContentTypesViewModel
{
    public IEnumerable<ContentTypeDefinition> Types { get; set; }
}
