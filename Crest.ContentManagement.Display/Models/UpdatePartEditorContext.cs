using Crest.ContentManagement.Metadata.Models;
using Crest.DisplayManagement.Handlers;

namespace Crest.ContentManagement.Display.Models;

public class UpdatePartEditorContext : BuildPartEditorContext
{

    public UpdatePartEditorContext(ContentTypePartDefinition typePartDefinition, UpdateEditorContext context)
        : base(typePartDefinition, context)
    {
    }

}
