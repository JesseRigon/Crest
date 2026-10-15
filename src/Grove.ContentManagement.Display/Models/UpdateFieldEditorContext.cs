using Grove.ContentManagement.Metadata.Models;
using Grove.DisplayManagement.Handlers;

namespace Grove.ContentManagement.Display.Models;

public class UpdateFieldEditorContext : BuildFieldEditorContext
{
    public UpdateFieldEditorContext(ContentPart contentPart, ContentTypePartDefinition typePartDefinition, ContentPartFieldDefinition partFieldDefinition, UpdateEditorContext context)
        : base(contentPart, typePartDefinition, partFieldDefinition, context)
    {
    }
}
