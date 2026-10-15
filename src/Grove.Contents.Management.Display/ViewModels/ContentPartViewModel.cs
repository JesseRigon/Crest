using Grove.Display.Management.Views;

namespace Grove.Contents.Management.Display.ViewModels;

public class ContentPartViewModel : ShapeViewModel
{
    public ContentPartViewModel()
    {
    }

    public ContentPartViewModel(ContentPart contentPart)
    {
        ContentPart = contentPart;
    }

    public ContentPart ContentPart { get; set; }
}
