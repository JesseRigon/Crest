using Crest.ContentManagement.Metadata.Models;
using Crest.DisplayManagement.Handlers;

namespace Crest.ContentTypes.Editors;

public interface IContentTypePartDefinitionDisplayDriver : IDisplayDriver<ContentTypePartDefinition, BuildDisplayContext, BuildEditorContext, UpdateTypePartEditorContext>;
