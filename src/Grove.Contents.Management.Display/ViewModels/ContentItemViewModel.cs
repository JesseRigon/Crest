using Grove.Display.Management.Views;

namespace Grove.Contents.Management.Display.ViewModels;

public class ContentItemViewModel : ShapeViewModel
{
    public ContentItemViewModel()
    {
    }

    public ContentItemViewModel(ContentItem contentItem)
    {
        ContentItem = contentItem;
    }

    public ContentItem ContentItem { get; set; }
}
