using Microsoft.AspNetCore.Razor.TagHelpers;
using Crest.DisplayManagement;
using Crest.DisplayManagement.TagHelpers;

namespace Crest.Contents.TagHelpers;

[HtmlTargetElement("contentitem")]
public class ContentItemTagHelper : BaseShapeTagHelper
{
    public ContentItemTagHelper(IShapeFactory shapeFactory, IDisplayHelper displayHelper)
        : base(shapeFactory, displayHelper)
    {
        Type = "ContentItem";
    }
}
