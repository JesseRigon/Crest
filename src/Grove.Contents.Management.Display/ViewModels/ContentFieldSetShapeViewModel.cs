using Grove.Contents.Management.Metadata.Models;
using Grove.Display.Management.Views;

namespace Grove.Contents.Management.Display.ViewModels;

public class ContentFieldSetShapeViewModel : ShapeViewModel
{
    public ContentFieldSetShapeViewModel()
    {
    }

    public ContentFieldSet ContentFieldSet { get; set; }

    public ContentTypeFieldSetDefinition ContentTypeFieldSetDefinition { get; set; }
}
