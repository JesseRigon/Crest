using Crest.DisplayManagement.Handlers;

namespace Crest.ContentManagement.Display.ContentDisplay;

public interface IContentDisplayDriver : IDisplayDriver<ContentItem, BuildDisplayContext, BuildEditorContext, UpdateEditorContext>;
