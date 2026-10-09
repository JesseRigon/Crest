using Crest.ContentManagement.Metadata.Models;
using Crest.DisplayManagement.Handlers;

namespace Crest.ContentTypes.Editors;

public interface IContentPartFieldDefinitionDisplayDriver : IDisplayDriver<ContentPartFieldDefinition, BuildDisplayContext, BuildEditorContext, UpdatePartFieldEditorContext>;
