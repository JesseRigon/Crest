using Grove.Environment.Extensions;

namespace Grove.DisplayManagement.Descriptors;

[FeatureTypeDiscovery(SkipExtension = true)]
public interface IShapeTableProvider
{
    ValueTask DiscoverAsync(ShapeTableBuilder builder);
}
