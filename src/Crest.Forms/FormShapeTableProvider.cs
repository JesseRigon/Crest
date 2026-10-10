using Crest.ContentManagement;
using Crest.DisplayManagement;
using Crest.DisplayManagement.Descriptors;
using Crest.DisplayManagement.Utilities;
using Crest.Forms.Models;

namespace Crest.Forms;

public class FormShapeTableProvider : IShapeTableProvider
{
    public ValueTask DiscoverAsync(ShapeTableBuilder builder)
    {
        builder.Describe("Widget__Form")
            .OnDisplaying(context =>
            {
                if (context.Shape.TryGetProperty<ContentItem>("ContentItem", out var contentItem)
                    && contentItem.TryGet<FormElementPart>(out var elementPart)
                    && !string.IsNullOrEmpty(elementPart.Id))
                {
                    context.Shape.Metadata.Alternates.Add($"Widget__{contentItem.ContentType}_{elementPart.Id.EncodeAlternateElement()}");
                }
            });

        return ValueTask.CompletedTask;
    }
}
