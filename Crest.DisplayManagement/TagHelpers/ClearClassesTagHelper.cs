using Microsoft.AspNetCore.Razor.TagHelpers;

namespace Crest.DisplayManagement.TagHelpers;

[HtmlTargetElement("clear-classes", TagStructure = TagStructure.WithoutEndTag)]
public class ClearClassesTagHelper : TagHelper
{
    public override Task ProcessAsync(TagHelperContext context, TagHelperOutput output)
    {
        var shape = (IShape)context.Items[typeof(IShape)];

        shape?.Classes.Clear();

        output.SuppressOutput();

        return Task.CompletedTask;
    }
}
