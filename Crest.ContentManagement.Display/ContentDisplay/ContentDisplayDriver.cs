using Crest.DisplayManagement.Handlers;

namespace Crest.ContentManagement.Display.ContentDisplay;

public abstract class ContentDisplayDriver : DisplayDriver<ContentItem, BuildDisplayContext, BuildEditorContext, UpdateEditorContext>, IContentDisplayDriver;
