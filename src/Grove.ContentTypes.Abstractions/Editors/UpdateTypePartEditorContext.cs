using Grove.ContentManagement.Metadata.Builders;
using Grove.DisplayManagement;
using Grove.DisplayManagement.ModelBinding;
using Grove.DisplayManagement.Zones;

namespace Grove.ContentTypes.Editors;

public class UpdateTypePartEditorContext : UpdateContentDefinitionEditorContext<ContentTypePartDefinitionBuilder>
{
    public UpdateTypePartEditorContext(
            ContentTypePartDefinitionBuilder builder,
            IShape model,
            string groupId,
            bool isNew,
            IShapeFactory shapeFactory,
            IZoneHolding layout,
            IUpdateModel updater)
        : base(builder, model, groupId, isNew, shapeFactory, layout, updater)
    {
    }
}
