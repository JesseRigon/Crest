using Grove.ContentManagement.Metadata.Models;
using Grove.DisplayManagement.Handlers;

namespace Grove.ContentTypes.Editors;

public interface IContentPartDefinitionDisplayDriver : IDisplayDriver<ContentPartDefinition, BuildDisplayContext, BuildEditorContext, UpdatePartEditorContext>;
