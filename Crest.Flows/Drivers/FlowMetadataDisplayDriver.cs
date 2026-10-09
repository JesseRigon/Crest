using Crest.ContentManagement;
using Crest.ContentManagement.Display.ContentDisplay;
using Crest.DisplayManagement.Handlers;
using Crest.DisplayManagement.Views;
using Crest.Flows.Models;

namespace Crest.Flows.Drivers;

public sealed class FlowMetadataDisplayDriver : ContentDisplayDriver
{
    public override IDisplayResult Edit(ContentItem model, BuildEditorContext context)
    {
        if (!model.TryGet<FlowMetadata>(out var flowMetadata))
        {
            return null;
        }

        return Initialize<FlowMetadata>("FlowMetadata_Edit", m =>
        {
            m.Alignment = flowMetadata.Alignment;
            m.Size = flowMetadata.Size;
        }).Location("Footer");
    }

    public override async Task<IDisplayResult> UpdateAsync(ContentItem contentItem, UpdateEditorContext context)
    {
        if (!contentItem.TryGet<FlowMetadata>(out _))
        {
            return null;
        }

        await contentItem.AlterAsync<FlowMetadata>(model => context.Updater.TryUpdateModelAsync(model, Prefix));

        return Edit(contentItem, context);
    }
}
