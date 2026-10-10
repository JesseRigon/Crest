using Microsoft.AspNetCore.Razor.TagHelpers;
using Crest.DisplayManagement;
using Crest.DisplayManagement.TagHelpers;

namespace Crest.Demo.TagHelpers;

[HtmlTargetElement("baz")]
public class BazTagHelper : BaseShapeTagHelper
{
    public BazTagHelper(IShapeFactory shapeFactory, IDisplayHelper displayHelper)
        : base(shapeFactory, displayHelper)
    {
        Type = "Baz";
    }
}
