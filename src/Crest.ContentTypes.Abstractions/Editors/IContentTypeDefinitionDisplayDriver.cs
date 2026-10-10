using Crest.ContentManagement.Metadata.Models;
using Crest.DisplayManagement.Handlers;

namespace Crest.ContentTypes.Editors;

public interface IContentTypeDefinitionDisplayDriver : IDisplayDriver<ContentTypeDefinition, BuildDisplayContext, BuildEditorContext, UpdateTypeEditorContext>;
