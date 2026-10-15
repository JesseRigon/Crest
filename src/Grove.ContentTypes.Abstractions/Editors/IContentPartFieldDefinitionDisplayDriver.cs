using Grove.ContentManagement.Metadata.Models;
using Grove.DisplayManagement.Handlers;

namespace Grove.ContentTypes.Editors;

public interface IContentPartFieldDefinitionDisplayDriver : IDisplayDriver<ContentPartFieldDefinition, BuildDisplayContext, BuildEditorContext, UpdatePartFieldEditorContext>;
