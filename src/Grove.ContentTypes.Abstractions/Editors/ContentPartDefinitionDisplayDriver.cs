using Grove.ContentManagement.Metadata.Models;
using Grove.DisplayManagement.Handlers;

namespace Grove.ContentTypes.Editors;

public abstract class ContentPartDefinitionDisplayDriver : DisplayDriver<ContentPartDefinition, BuildDisplayContext, BuildEditorContext, UpdatePartEditorContext>, IContentPartDefinitionDisplayDriver
{
    public override bool CanHandleModel(ContentPartDefinition model)
    {
        return true;
    }
}
