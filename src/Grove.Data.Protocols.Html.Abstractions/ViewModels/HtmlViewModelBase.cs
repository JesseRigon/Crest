using Microsoft.AspNetCore.Mvc.ModelBinding;
using Grove.Contents.Management;

namespace Grove.Data.Protocols.Html.ViewModels;

public abstract class HtmlViewModelBase
{
    public string Html { get; set; }

    [BindNever]
    public ContentItem ContentItem { get; set; }    
}
