namespace Grove.Display.Management.Descriptors.ShapePlacementStrategy;

public interface IPlacementNodeFilterProvider
{
    string Key { get; }
    bool IsMatch(ShapePlacementContext context, object expression);
}
