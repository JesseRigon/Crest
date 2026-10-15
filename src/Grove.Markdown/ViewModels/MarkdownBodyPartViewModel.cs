using Microsoft.AspNetCore.Mvc.ModelBinding;
using Grove.ContentManagement;
using Grove.ContentManagement.Metadata.Models;
using Grove.Markdown.Models;

namespace Grove.Markdown.ViewModels;

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
