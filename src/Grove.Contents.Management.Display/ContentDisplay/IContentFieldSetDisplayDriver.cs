using Grove.Contents.Management.Metadata.Models;
using Grove.Display.Management.Handlers;
using Grove.Display.Management.Views;

namespace Grove.Contents.Management.Display.ContentDisplay;

public interface IContentFieldSetDisplayDriver
{
    Task<IDisplayResult> BuildDisplayAsync(ContentFieldSet contentFieldSet, ContentTypeFieldSetDefinition typeFieldSetDefinition, BuildDisplayContext context);
}
