using Crest.Alias.Models;
using Crest.ContentManagement;
using Crest.DisplayManagement;
using Crest.DisplayManagement.Descriptors;

namespace Crest.Alias.Services;

public sealed class WidgetAliasShapeTableProvider : ShapeTableProvider
{
    public override ValueTask DiscoverAsync(ShapeTableBuilder builder)
    {
        builder.Describe("Widget")
            .OnDisplaying(displaying =>
            {
                var shape = displaying.Shape;
                var contentItem = shape.GetProperty<ContentItem>("ContentItem");

                if (contentItem is not null && contentItem.TryGet<AliasPart>(out var aliasPart))
                {
                    var displayType = displaying.Shape.Metadata.DisplayType;

                    // Get cached alternates and add them efficiently
                    var cachedAlternates = WidgetAliasAlternatesFactory.GetAlternates(
                        aliasPart.Alias,
                        displayType);

                    displaying.Shape.Metadata.Alternates.AddRange(cachedAlternates);
                }
            });

        return ValueTask.CompletedTask;
    }
}
