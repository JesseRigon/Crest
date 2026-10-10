using Crest.ContentManagement;
using Crest.ContentManagement.Display.ContentDisplay;
using Crest.DisplayManagement.Handlers;
using Crest.DisplayManagement.Views;
using Crest.Forms.Models;

namespace Crest.Forms.Drivers;

public sealed class FormContentDisplayDriver : ContentDisplayDriver
{
    public override Task<IDisplayResult> DisplayAsync(ContentItem model, BuildDisplayContext context)
    {
        var formItemShape = context.Shape;
        // If the content item contains FormPart add Form Wrapper only in Display type Detail
        if (model.TryGet<FormPart>(out _) && context.DisplayType == PlatformConstants.DisplayType.Detail)
        {
            // Add wrapper for content type if template is not available it will fall back to Form_Wrapper
            formItemShape.Metadata.Wrappers.Add($"Form_Wrapper__{model.ContentType}");
        }

        // We don't need to return a shape result
        return Task.FromResult<IDisplayResult>(null);
    }
}
