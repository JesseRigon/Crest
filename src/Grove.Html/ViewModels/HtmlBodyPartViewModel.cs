using Microsoft.AspNetCore.Mvc.ModelBinding;
using Grove.ContentManagement.Metadata.Models;
using Grove.Html.Models;

namespace Grove.Html.ViewModels;

public class HtmlBodyPartViewModel : HtmlViewModelBase
{
    [BindNever]
    public HtmlBodyPart HtmlBodyPart { get; set; }

    [BindNever]
    public ContentTypePartDefinition TypePartDefinition { get; set; }
}
