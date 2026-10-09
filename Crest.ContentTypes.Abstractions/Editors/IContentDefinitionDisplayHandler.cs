using Crest.ContentManagement.Metadata.Models;
using Crest.DisplayManagement.Handlers;

namespace Crest.ContentTypes.Editors;

public interface IContentDefinitionDisplayHandler
{
    Task BuildTypeEditorAsync(ContentTypeDefinition definition, BuildEditorContext context);
    Task UpdateTypeEditorAsync(ContentTypeDefinition definition, UpdateTypeEditorContext context);

    Task BuildTypePartEditorAsync(ContentTypePartDefinition definition, BuildEditorContext context);
    Task UpdateTypePartEditorAsync(ContentTypePartDefinition definition, UpdateTypePartEditorContext context);

    Task BuildPartEditorAsync(ContentPartDefinition definition, BuildEditorContext context);
    Task UpdatePartEditorAsync(ContentPartDefinition definition, UpdatePartEditorContext context);

    Task BuildPartFieldEditorAsync(ContentPartFieldDefinition definition, BuildEditorContext context);
    Task UpdatePartFieldEditorAsync(ContentPartFieldDefinition definition, UpdatePartFieldEditorContext context);
}
