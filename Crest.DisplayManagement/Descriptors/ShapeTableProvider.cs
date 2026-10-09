namespace Crest.DisplayManagement.Descriptors;

public abstract class ShapeTableProvider : IShapeTableProvider
{
    public abstract ValueTask DiscoverAsync(ShapeTableBuilder builder);
}
