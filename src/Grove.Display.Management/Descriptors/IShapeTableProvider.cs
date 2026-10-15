using Grove.Environment.Extensions;

namespace Grove.Display.Management.Descriptors;

[FeatureTypeDiscovery(SkipExtension = true)]
public interface IShapeTableProvider
{
    ValueTask DiscoverAsync(ShapeTableBuilder builder);
}
