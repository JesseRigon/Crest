using Grove.Contents.Management.Display.ContentDisplay;
using Grove.Contents.Management.Display.Models;
using Grove.Display.Management.Views;
using Grove.Display.Templates.Models;

namespace Grove.Display.Templates.Drivers;

/// <summary>
/// The part's shape: the stored tree, which <see cref="Services.DisplayTreePartBinding"/> turns
/// into nodes while the item's tree is built. No MVC editor: the Blazor editor tree places the
/// <c>DisplayTreeEditor</c> component for the part.
/// </summary>
public sealed class DisplayTreePartDisplayDriver : ContentPartDisplayDriver<DisplayTreePart>
{
    public override IDisplayResult Display(DisplayTreePart part, BuildPartDisplayContext context) =>
        Dynamic(GetDisplayShapeType(context), shape =>
        {
            shape.Tree = part.Tree;
            shape.ContentItem = part.ContentItem;
        })
        .Location(PlatformConstants.DisplayType.Detail, "Content")
        .Location(PlatformConstants.DisplayType.Summary, "Content");
}
