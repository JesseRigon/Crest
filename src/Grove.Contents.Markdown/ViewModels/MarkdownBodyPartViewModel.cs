using Microsoft.AspNetCore.Mvc.ModelBinding;
using Grove.Contents.Management;
using Grove.Contents.Management.Metadata.Models;
using Grove.Contents.Markdown.Models;

namespace Grove.Contents.Markdown.ViewModels;

public class MarkdownBodyPartViewModel
{
    public string Markdown { get; set; }
    public string Html { get; set; }

    [BindNever]
    public ContentItem ContentItem { get; set; }

    [BindNever]
    public MarkdownBodyPart MarkdownBodyPart { get; set; }

    [BindNever]
    public ContentTypePartDefinition TypePartDefinition { get; set; }
}
