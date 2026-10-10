using Crest.ContentManagement.Metadata.Models;
using Crest.DisplayManagement.Views;

namespace Crest.ContentManagement.Display.ViewModels;

public class ContentPartShapeViewModel : ShapeViewModel
{
    public ContentPartShapeViewModel()
    {
    }

    public ContentPart ContentPart { get; set; }

    public ContentTypePartDefinition ContentTypePartDefinition { get; set; }
}
