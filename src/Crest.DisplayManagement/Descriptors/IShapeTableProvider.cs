using Crest.Environment.Extensions;

namespace Crest.DisplayManagement.Descriptors;

[FeatureTypeDiscovery(SkipExtension = true)]
public interface IShapeTableProvider
{
    ValueTask DiscoverAsync(ShapeTableBuilder builder);
}
