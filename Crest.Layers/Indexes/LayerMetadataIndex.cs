using Crest.ContentManagement;
using Crest.Layers.Models;
using YesSql.Indexes;

namespace Crest.Layers.Indexes;

public class LayerMetadataIndex : MapIndex
{
    public string Zone { get; set; }
}

public class LayerMetadataIndexProvider : IndexProvider<ContentItem>
{
    public override void Describe(DescribeContext<ContentItem> context)
    {
        context.For<LayerMetadataIndex>()
            .When(contentItem => contentItem.Has<LayerMetadata>())
            .Map(contentItem =>
            {
                if (!contentItem.TryGet<LayerMetadata>(out var layerMetadata))
                {
                    return null;
                }

                return new LayerMetadataIndex
                {
                    Zone = layerMetadata.Zone,
                };
            });
    }
}
