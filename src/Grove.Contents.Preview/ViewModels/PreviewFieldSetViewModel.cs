using Microsoft.AspNetCore.Mvc.ModelBinding;
using Grove.Contents.Management;
using Grove.Contents.Preview.Models;

namespace Grove.Contents.Preview.ViewModels;

public class PreviewFieldSetViewModel
{
    public string Pattern { get; set; }

    [BindNever]
    public ContentItem ContentItem { get; set; }

    [BindNever]
    public PreviewFieldSet PreviewFieldSet { get; set; }
}
