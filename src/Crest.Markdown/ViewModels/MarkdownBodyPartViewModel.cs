using Microsoft.AspNetCore.Mvc.ModelBinding;
using Crest.ContentManagement;
using Crest.ContentManagement.Metadata.Models;
using Crest.Markdown.Models;

namespace Crest.Markdown.ViewModels;

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
