using Grove.Display.Management.Zones;

namespace Grove.Display.Management.Handlers;

public class BuildDisplayContext : BuildShapeContext
{
    public BuildDisplayContext(IShape shape, string displayType, string groupId, IShapeFactory shapeFactory, IZoneHolding layout)
        : base(shape, groupId, shapeFactory, layout)
    {
        DisplayType = displayType;
    }

    public string DisplayType { get; private set; }
}
