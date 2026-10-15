using Microsoft.AspNetCore.Mvc.ModelBinding;
using Grove.Contents.Management.Metadata.Models;
using Grove.Data.Protocols.Html.Models;

namespace Grove.Data.Protocols.Html.ViewModels;

public class HtmlBodyPartViewModel : HtmlViewModelBase
{
    [BindNever]
    public HtmlBodyPart HtmlBodyPart { get; set; }

    [BindNever]
    public ContentTypePartDefinition TypePartDefinition { get; set; }
}
