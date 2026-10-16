using Grove.Contents.Management.Metadata.Models;
using Grove.Display.Management.Handlers;
using Grove.Display.Management.Views;

namespace Grove.Contents.Management.Display.ContentDisplay;

public interface IContentFieldDisplayDriver
{
    Task<IDisplayResult> BuildDisplayAsync(ContentFieldSet contentFieldSet, ContentFieldDefinition partFieldDefinition, ContentTypeFieldSetDefinition typeFieldSetDefinition, BuildDisplayContext context);
}
