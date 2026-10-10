using Crest.ContentManagement;
using Crest.ContentManagement.Metadata.Models;

namespace Crest.Flows.ViewModels;

public class BagPartWidgetViewModel
{
    public ContentItem ContentItem { get; set; }

    public ContentTypeDefinition ContentTypeDefinition { get; set; }

    public bool Editable { get; set; }

    public bool Viewable { get; set; }

    public bool Deletable { get; set; }
}
