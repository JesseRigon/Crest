using Grove.ContentManagement;
using Grove.ContentManagement.Display.ContentDisplay;
using Grove.ContentManagement.Display.ViewModels;
using Grove.DisplayManagement.Handlers;
using Grove.DisplayManagement.Views;

namespace Grove.ContentPreview.Drivers;

public sealed class ContentPreviewDriver : ContentDisplayDriver
{
    public override IDisplayResult Edit(ContentItem contentItem, BuildEditorContext context)
    {
        return Factory("ContentPreview_Button", static (ContentItem item) => new ContentItemViewModel(item), contentItem)
            .Location("Actions:after");
    }
}
