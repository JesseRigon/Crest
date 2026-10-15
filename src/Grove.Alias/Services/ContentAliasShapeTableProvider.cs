using Grove.Alias.Models;
using Grove.ContentManagement;
using Grove.DisplayManagement;
using Grove.DisplayManagement.Descriptors;

namespace Grove.Alias.Services;

public sealed class ContentAliasShapeTableProvider : ShapeTableProvider
{
    public override ValueTask DiscoverAsync(ShapeTableBuilder builder)
    {
        builder.Describe("Content")
            .OnDisplaying(displaying =>
            {
                var shape = displaying.Shape;
                var contentItem = shape.GetProperty<ContentItem>("ContentItem");

                if (contentItem?.TryGet<AliasPart>(out var aliasPart) == true)
                {
                    var displayType = displaying.Shape.Metadata.DisplayType;

                    // Get cached alternates and add them efficiently
                    var cachedAlternates = AliasAlternatesFactory.GetAlternates(
                        aliasPart.Alias,
                        displayType);

                    displaying.Shape.Metadata.Alternates.AddRange(cachedAlternates);
                }
            });

        return ValueTask.CompletedTask;
    }
}
