using Grove.ContentManagement.Display.ContentDisplay;
using Grove.ContentManagement.Display.Models;
using Grove.DisplayManagement.Views;
using Grove.Lists.Models;
using Grove.Modules;

namespace Grove.Lists.RemotePublishing;

[RequireFeatures("Grove.RemotePublishing")]
public sealed class ListMetaWeblogDriver : ContentPartDisplayDriver<ListPart>
{
    public override IDisplayResult Display(ListPart listPart, BuildPartDisplayContext context)
    {
        return Dynamic("ListPart_RemotePublishing", static (shape, listPart) =>
        {
            shape.ContentItem = listPart.ContentItem;
        }, listPart).Location("Content");
    }
}
