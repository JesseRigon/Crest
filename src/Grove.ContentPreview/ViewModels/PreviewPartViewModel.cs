using Microsoft.AspNetCore.Mvc.ModelBinding;
using Grove.ContentManagement;
using Grove.ContentPreview.Models;

namespace Grove.ContentPreview.ViewModels;

public class PreviewPartViewModel
{
    public string Pattern { get; set; }

    [BindNever]
    public ContentItem ContentItem { get; set; }

    [BindNever]
    public PreviewPart PreviewPart { get; set; }
}
