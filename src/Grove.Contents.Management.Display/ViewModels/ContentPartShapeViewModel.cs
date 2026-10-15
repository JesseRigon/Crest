using Grove.Contents.Management.Metadata.Models;
using Grove.Display.Management.Views;

namespace Grove.Contents.Management.Display.ViewModels;

public class ContentPartShapeViewModel : ShapeViewModel
{
    public ContentPartShapeViewModel()
    {
    }

    public ContentPart ContentPart { get; set; }

    public ContentTypePartDefinition ContentTypePartDefinition { get; set; }
}
