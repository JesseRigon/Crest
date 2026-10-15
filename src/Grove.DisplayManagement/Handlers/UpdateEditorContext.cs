using Grove.DisplayManagement.ModelBinding;
using Grove.DisplayManagement.Zones;

namespace Grove.DisplayManagement.Handlers;

public class UpdateEditorContext : BuildEditorContext
{

    public UpdateEditorContext(IShape model, string groupId, bool isNew, string htmlFieldPrefix, IShapeFactory shapeFactory,
        IZoneHolding layout, IUpdateModel updater)
        : base(model, groupId, isNew, htmlFieldPrefix, shapeFactory, layout, updater)
    {
    }

}
