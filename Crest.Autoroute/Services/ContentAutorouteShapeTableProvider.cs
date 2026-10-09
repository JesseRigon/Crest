using Crest.Autoroute.Models;
using Crest.ContentManagement;
using Crest.DisplayManagement;
using Crest.DisplayManagement.Descriptors;

namespace Crest.Autoroute.Services;

public sealed class ContentAutorouteShapeTableProvider : ShapeTableProvider
{
    public override ValueTask DiscoverAsync(ShapeTableBuilder builder)
    {
        builder.Describe("Content")
            .OnDisplaying(displaying =>
            {
                var shape = displaying.Shape;
                var contentItem = shape.GetProperty<ContentItem>("ContentItem");

                if (contentItem is not null && contentItem.TryGet<AutoroutePart>(out var autoroutePart))
                {
                    var displayType = displaying.Shape.Metadata.DisplayType;

                    // Get cached alternates and add them efficiently
                    var cachedAlternates = AutorouteAlternatesFactory.GetAlternates(
                        autoroutePart.Path,
                        displayType);

                    displaying.Shape.Metadata.Alternates.AddRange(cachedAlternates);
                }
            });

        return ValueTask.CompletedTask;
    }
}
