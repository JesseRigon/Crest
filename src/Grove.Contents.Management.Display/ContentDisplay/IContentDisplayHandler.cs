using Grove.Display.Management.Handlers;

namespace Grove.Contents.Management.Display.ContentDisplay;

/// <summary>
/// An implementation of <see cref="IContentDisplayHandler"/> is able to take part in the rendering of
/// a <see cref="ContentItem"/> instance.
/// </summary>
public interface IContentDisplayHandler
{
    Task BuildDisplayAsync(ContentItem contentItem, BuildDisplayContext context);
}
