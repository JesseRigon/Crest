using Grove.ContentManagement.Metadata.Models;
using Grove.DisplayManagement.Handlers;

namespace Grove.ContentTypes.Editors;

public interface IContentTypePartDefinitionDisplayDriver : IDisplayDriver<ContentTypePartDefinition, BuildDisplayContext, BuildEditorContext, UpdateTypePartEditorContext>;
