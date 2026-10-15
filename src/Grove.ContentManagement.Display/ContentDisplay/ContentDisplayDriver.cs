using Grove.DisplayManagement.Handlers;

namespace Grove.ContentManagement.Display.ContentDisplay;

public abstract class ContentDisplayDriver : DisplayDriver<ContentItem, BuildDisplayContext, BuildEditorContext, UpdateEditorContext>, IContentDisplayDriver;
