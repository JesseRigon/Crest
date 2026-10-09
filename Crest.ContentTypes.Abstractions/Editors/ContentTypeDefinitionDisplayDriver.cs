using Crest.ContentManagement.Metadata.Models;
using Crest.DisplayManagement.Handlers;

namespace Crest.ContentTypes.Editors;

public abstract class ContentTypeDefinitionDisplayDriver : DisplayDriver<ContentTypeDefinition, BuildDisplayContext, BuildEditorContext, UpdateTypeEditorContext>, IContentTypeDefinitionDisplayDriver
{
    public override bool CanHandleModel(ContentTypeDefinition model)
    {
        return true;
    }
}
