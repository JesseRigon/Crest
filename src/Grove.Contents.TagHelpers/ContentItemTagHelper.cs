using Microsoft.AspNetCore.Razor.TagHelpers;
using Grove.DisplayManagement;
using Grove.DisplayManagement.TagHelpers;

namespace Grove.Contents.TagHelpers;

[HtmlTargetElement("contentitem")]
public class ContentItemTagHelper : BaseShapeTagHelper
{
    public ContentItemTagHelper(IShapeFactory shapeFactory, IDisplayHelper displayHelper)
        : base(shapeFactory, displayHelper)
    {
        Type = "ContentItem";
    }
}
