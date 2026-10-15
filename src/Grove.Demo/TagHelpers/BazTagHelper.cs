using Microsoft.AspNetCore.Razor.TagHelpers;
using Grove.DisplayManagement;
using Grove.DisplayManagement.TagHelpers;

namespace Grove.Demo.TagHelpers;

[HtmlTargetElement("baz")]
public class BazTagHelper : BaseShapeTagHelper
{
    public BazTagHelper(IShapeFactory shapeFactory, IDisplayHelper displayHelper)
        : base(shapeFactory, displayHelper)
    {
        Type = "Baz";
    }
}
