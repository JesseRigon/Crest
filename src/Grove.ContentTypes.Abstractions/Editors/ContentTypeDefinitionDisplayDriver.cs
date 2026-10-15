using Grove.ContentManagement.Metadata.Models;
using Grove.DisplayManagement.Handlers;

namespace Grove.ContentTypes.Editors;

public abstract class ContentTypeDefinitionDisplayDriver : DisplayDriver<ContentTypeDefinition, BuildDisplayContext, BuildEditorContext, UpdateTypeEditorContext>, IContentTypeDefinitionDisplayDriver
{
    public override bool CanHandleModel(ContentTypeDefinition model)
    {
        return true;
    }
}
