using Microsoft.AspNetCore.Mvc.ModelBinding;
using Crest.ContentManagement.Metadata.Models;
using Crest.Html.Models;

namespace Crest.Html.ViewModels;

public class HtmlBodyPartViewModel : HtmlViewModelBase
{
    [BindNever]
    public HtmlBodyPart HtmlBodyPart { get; set; }

    [BindNever]
    public ContentTypePartDefinition TypePartDefinition { get; set; }
}
