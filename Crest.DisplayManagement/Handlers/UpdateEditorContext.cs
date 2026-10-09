using Crest.DisplayManagement.ModelBinding;
using Crest.DisplayManagement.Zones;

namespace Crest.DisplayManagement.Handlers;

public class UpdateEditorContext : BuildEditorContext
{

    public UpdateEditorContext(IShape model, string groupId, bool isNew, string htmlFieldPrefix, IShapeFactory shapeFactory,
        IZoneHolding layout, IUpdateModel updater)
        : base(model, groupId, isNew, htmlFieldPrefix, shapeFactory, layout, updater)
    {
    }

}
