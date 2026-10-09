using Crest.ContentManagement.Metadata.Models;
using Crest.DisplayManagement.Handlers;

namespace Crest.ContentTypes.Editors;

public abstract class ContentPartDefinitionDisplayDriver : DisplayDriver<ContentPartDefinition, BuildDisplayContext, BuildEditorContext, UpdatePartEditorContext>, IContentPartDefinitionDisplayDriver
{
    public override bool CanHandleModel(ContentPartDefinition model)
    {
        return true;
    }
}
