using Grove.Display.Management.Views;

namespace Grove.Contents.Management.Display.ViewModels;

public class ContentFieldSetViewModel : ShapeViewModel
{
    public ContentFieldSetViewModel()
    {
    }

    public ContentFieldSetViewModel(ContentFieldSet contentFieldSet)
    {
        ContentFieldSet = contentFieldSet;
    }

    public ContentFieldSet ContentFieldSet { get; set; }
}
