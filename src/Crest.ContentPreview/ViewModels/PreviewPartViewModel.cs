using Microsoft.AspNetCore.Mvc.ModelBinding;
using Crest.ContentManagement;
using Crest.ContentPreview.Models;

namespace Crest.ContentPreview.ViewModels;

public class PreviewPartViewModel
{
    public string Pattern { get; set; }

    [BindNever]
    public ContentItem ContentItem { get; set; }

    [BindNever]
    public PreviewPart PreviewPart { get; set; }
}
