using Grove.Display.Management;

namespace Grove.Contents.Management.Display;

/// <summary>
/// Describes services responsible for displaying a content item. The result dynamic objects
/// are the Shape to render a <see cref="ContentItem"/>.
/// </summary>
public interface IContentItemDisplayManager
{
    Task<IShape> BuildDisplayAsync(ContentItem content, string displayType = "", string groupId = "");
}
