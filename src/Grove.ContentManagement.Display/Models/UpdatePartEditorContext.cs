using Grove.ContentManagement.Metadata.Models;
using Grove.DisplayManagement.Handlers;

namespace Grove.ContentManagement.Display.Models;

public class UpdatePartEditorContext : BuildPartEditorContext
{

    public UpdatePartEditorContext(ContentTypePartDefinition typePartDefinition, UpdateEditorContext context)
        : base(typePartDefinition, context)
    {
    }

}
