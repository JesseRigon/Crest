using Crest.DisplayManagement;
using Crest.DisplayManagement.Handlers;
using Crest.DisplayManagement.ModelBinding;
using Crest.DisplayManagement.Zones;

namespace Crest.ContentTypes.Editors;

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
