using Grove.ContentManagement.Metadata.Models;
using Grove.DisplayManagement.Views;

namespace Grove.ContentManagement.Display.ViewModels;

public class ContentPartShapeViewModel : ShapeViewModel
{
    public ContentPartShapeViewModel()
    {
    }

    public ContentPart ContentPart { get; set; }

    public ContentTypePartDefinition ContentTypePartDefinition { get; set; }
}
