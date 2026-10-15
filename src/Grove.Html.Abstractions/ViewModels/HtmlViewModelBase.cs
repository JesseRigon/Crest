using Microsoft.AspNetCore.Mvc.ModelBinding;
using Grove.ContentManagement;

namespace Grove.Html.ViewModels;

public abstract class HtmlViewModelBase
{
    public string Html { get; set; }

    [BindNever]
    public ContentItem ContentItem { get; set; }    
}
