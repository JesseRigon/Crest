using Microsoft.AspNetCore.Razor.TagHelpers;
using Crest.DisplayManagement;
using Crest.DisplayManagement.TagHelpers;

namespace Crest.Menu.TagHelpers;

[HtmlTargetElement("menu")]
public class MenuTagHelper : BaseShapeTagHelper
{
    public MenuTagHelper(IShapeFactory shapeFactory, IDisplayHelper displayHelper)
        : base(shapeFactory, displayHelper)
    {
        Type = "Menu";
    }
}
