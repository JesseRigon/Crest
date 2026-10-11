using Microsoft.AspNetCore.Mvc.ModelBinding;
using Crest.ContentManagement;

namespace Crest.Html.ViewModels;

public abstract class HtmlViewModelBase
{
    public string Html { get; set; }

    [BindNever]
    public ContentItem ContentItem { get; set; }    
}
