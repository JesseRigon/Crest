using Microsoft.AspNetCore.Razor.TagHelpers;
using Grove.DisplayManagement;
using Grove.DisplayManagement.TagHelpers;

namespace Grove.Menu.TagHelpers;

[HtmlTargetElement("menu")]
public class MenuTagHelper : BaseShapeTagHelper
{
    public MenuTagHelper(IShapeFactory shapeFactory, IDisplayHelper displayHelper)
        : base(shapeFactory, displayHelper)
    {
        Type = "Menu";
    }
}
