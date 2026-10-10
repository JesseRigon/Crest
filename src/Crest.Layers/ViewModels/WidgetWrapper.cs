using Crest.ContentManagement;
using Crest.DisplayManagement;
using Crest.DisplayManagement.Views;

namespace Crest.Layers.ViewModels;

public class WidgetWrapper : ShapeViewModel
{
    public WidgetWrapper() : base("Widget_Wrapper")
    {
    }

    public ContentItem Widget { get; set; }
    public IShape Content { get; set; }
}
