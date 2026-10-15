using Grove.ContentManagement.Metadata.Models;
using Grove.DisplayManagement.Handlers;
using Grove.DisplayManagement.Views;

namespace Grove.ContentManagement.Display.ContentDisplay;

public interface IContentFieldDisplayDriver
{
    Task<IDisplayResult> BuildDisplayAsync(ContentPart contentPart, ContentPartFieldDefinition partFieldDefinition, ContentTypePartDefinition typePartDefinition, BuildDisplayContext context);
    Task<IDisplayResult> BuildEditorAsync(ContentPart contentPart, ContentPartFieldDefinition partFieldDefinition, ContentTypePartDefinition typePartDefinition, BuildEditorContext context);
    Task<IDisplayResult> UpdateEditorAsync(ContentPart contentPart, ContentPartFieldDefinition partFieldDefinition, ContentTypePartDefinition typePartDefinition, UpdateEditorContext context);
}
