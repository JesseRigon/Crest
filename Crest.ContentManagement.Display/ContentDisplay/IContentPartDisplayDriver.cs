using Crest.ContentManagement.Metadata.Models;
using Crest.DisplayManagement.Handlers;
using Crest.DisplayManagement.Views;

namespace Crest.ContentManagement.Display.ContentDisplay;

public interface IContentPartDisplayDriver
{
    Task<IDisplayResult> BuildDisplayAsync(ContentPart contentPart, ContentTypePartDefinition typePartDefinition, BuildDisplayContext context);
    Task<IDisplayResult> BuildEditorAsync(ContentPart contentPart, ContentTypePartDefinition typePartDefinition, BuildEditorContext context);
    Task<IDisplayResult> UpdateEditorAsync(ContentPart contentPart, ContentTypePartDefinition typePartDefinition, UpdateEditorContext context);
}
