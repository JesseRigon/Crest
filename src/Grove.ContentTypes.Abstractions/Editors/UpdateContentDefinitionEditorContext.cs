using Grove.DisplayManagement;
using Grove.DisplayManagement.Handlers;
using Grove.DisplayManagement.ModelBinding;
using Grove.DisplayManagement.Zones;

namespace Grove.ContentTypes.Editors;

public class UpdateContentDefinitionEditorContext<TBuilder> : UpdateEditorContext
{
    public UpdateContentDefinitionEditorContext(
        TBuilder builder,
        IShape model,
        string groupId,
        bool isNew,
        IShapeFactory shapeFactory,
        IZoneHolding layout,
        IUpdateModel updater)
        : base(model, groupId, isNew, "", shapeFactory, layout, updater)
    {
        Builder = builder;
    }

    public TBuilder Builder { get; private set; }
}
