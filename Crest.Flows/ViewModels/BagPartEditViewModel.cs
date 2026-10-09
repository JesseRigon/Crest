using System.Runtime.Serialization;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Crest.ContentManagement.Metadata.Models;
using Crest.DisplayManagement.ModelBinding;
using Crest.Flows.Models;

namespace Crest.Flows.ViewModels;

public class BagPartEditViewModel
{
    public string[] Prefixes { get; set; } = [];
    public string[] ContentTypes { get; set; } = [];
    public string[] ContentItems { get; set; } = [];

    [BindNever]
    public BagPart BagPart { get; set; }

    [IgnoreDataMember]
    [BindNever]
    public IUpdateModel Updater { get; set; }

    [BindNever]
    public IEnumerable<ContentTypeDefinition> ContainedContentTypeDefinitions { get; set; }

    [BindNever]
    public IEnumerable<BagPartWidgetViewModel> AccessibleWidgets { get; set; }

    [BindNever]
    public ContentTypePartDefinition TypePartDefinition { get; set; }

    [BindNever]
    public string AddButtonText { get; set; }

    [BindNever]
    public string ModalTitleText { get; set; }
}
