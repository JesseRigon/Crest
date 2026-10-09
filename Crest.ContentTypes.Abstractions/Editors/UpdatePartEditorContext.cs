using Crest.ContentManagement.Metadata.Builders;
using Crest.DisplayManagement;
using Crest.DisplayManagement.ModelBinding;
using Crest.DisplayManagement.Zones;

namespace Crest.ContentTypes.Editors;

public class UpdatePartEditorContext : UpdateContentDefinitionEditorContext<ContentPartDefinitionBuilder>
{
    public UpdatePartEditorContext(
            ContentPartDefinitionBuilder builder,
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
