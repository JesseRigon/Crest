using Crest.DisplayManagement.ModelBinding;
using Crest.DisplayManagement.Zones;

namespace Crest.DisplayManagement.Handlers;

public class BuildDisplayContext : BuildShapeContext
{
    public BuildDisplayContext(IShape shape, string displayType, string groupId, IShapeFactory shapeFactory, IZoneHolding layout, IUpdateModel updater)
        : base(shape, groupId, shapeFactory, layout, updater)
    {
        DisplayType = displayType;
    }

    public string DisplayType { get; private set; }
}
