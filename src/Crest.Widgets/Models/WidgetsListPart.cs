using Crest.ContentManagement;

namespace Crest.Widgets.Models;

// A content item with this part can have widget instances.
public class WidgetsListPart : ContentPart
{
    public Dictionary<string, List<ContentItem>> Widgets { get; set; } = [];
}
