using Crest.ContentManagement.Display.ContentDisplay;
using Crest.ContentManagement.Display.Models;
using Crest.DisplayManagement.Views;
using Crest.Lists.Models;
using Crest.Modules;

namespace Crest.Lists.RemotePublishing;

[RequireFeatures("Crest.RemotePublishing")]
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
