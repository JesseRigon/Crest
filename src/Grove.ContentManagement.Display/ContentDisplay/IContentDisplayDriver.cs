using Grove.DisplayManagement.Handlers;

namespace Grove.ContentManagement.Display.ContentDisplay;

public interface IContentDisplayDriver : IDisplayDriver<ContentItem, BuildDisplayContext, BuildEditorContext, UpdateEditorContext>;
