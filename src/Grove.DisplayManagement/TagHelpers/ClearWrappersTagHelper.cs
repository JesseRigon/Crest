using Microsoft.AspNetCore.Razor.TagHelpers;

namespace Grove.DisplayManagement.TagHelpers;

[HtmlTargetElement("clear-wrappers", ParentTag = ShapeMetadataTagHelper.TagName, TagStructure = TagStructure.WithoutEndTag)]
public class ClearWrappersTagHelper : TagHelper
{
    public override Task ProcessAsync(TagHelperContext context, TagHelperOutput output)
    {
        var shape = (IShape)context.Items[typeof(IShape)];

        shape?.Metadata.Wrappers.Clear();

        output.SuppressOutput();

        return Task.CompletedTask;
    }
}
