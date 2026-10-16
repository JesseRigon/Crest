using Microsoft.AspNetCore.Mvc.ModelBinding;
using Grove.Contents.Management.Metadata.Models;
using Grove.Data.Protocols.Html.Models;

namespace Grove.Data.Protocols.Html.ViewModels;

public class HtmlBodyFieldSetViewModel : HtmlViewModelBase
{
    [BindNever]
    public HtmlBodyFieldSet HtmlBodyFieldSet { get; set; }

    [BindNever]
    public ContentTypeFieldSetDefinition TypeFieldSetDefinition { get; set; }
}
