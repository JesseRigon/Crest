using Crest.ContentManagement.Metadata.Models;
using Crest.DisplayManagement.Handlers;

namespace Crest.ContentManagement.Display.Models;

public class UpdateFieldEditorContext : BuildFieldEditorContext
{
    public UpdateFieldEditorContext(ContentPart contentPart, ContentTypePartDefinition typePartDefinition, ContentPartFieldDefinition partFieldDefinition, UpdateEditorContext context)
        : base(contentPart, typePartDefinition, partFieldDefinition, context)
    {
    }
}
