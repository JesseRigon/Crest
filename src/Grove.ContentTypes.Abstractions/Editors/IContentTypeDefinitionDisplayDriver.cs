using Grove.ContentManagement.Metadata.Models;
using Grove.DisplayManagement.Handlers;

namespace Grove.ContentTypes.Editors;

public interface IContentTypeDefinitionDisplayDriver : IDisplayDriver<ContentTypeDefinition, BuildDisplayContext, BuildEditorContext, UpdateTypeEditorContext>;
