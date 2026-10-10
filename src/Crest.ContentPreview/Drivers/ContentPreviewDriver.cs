using Crest.ContentManagement;
using Crest.ContentManagement.Display.ContentDisplay;
using Crest.ContentManagement.Display.ViewModels;
using Crest.DisplayManagement.Handlers;
using Crest.DisplayManagement.Views;

namespace Crest.ContentPreview.Drivers;

public sealed class ContentPreviewDriver : ContentDisplayDriver
{
    public override IDisplayResult Edit(ContentItem contentItem, BuildEditorContext context)
    {
        return Factory("ContentPreview_Button", static (ContentItem item) => new ContentItemViewModel(item), contentItem)
            .Location("Actions:after");
    }
}
