using System.Runtime.Serialization;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Crest.ContentManagement;
using Crest.ContentManagement.Display.Models;
using Crest.Flows.Models;

namespace Crest.Flows.ViewModels;

public class BagPartViewModel
{
    public BagPart BagPart { get; set; }
    public IEnumerable<ContentItem> ContentItems => BagPart.ContentItems;

    [IgnoreDataMember]
    [BindNever]
    public BuildPartDisplayContext BuildPartDisplayContext { get; set; }

    public BagPartSettings Settings { get; set; }
    public string DisplayType => Settings?.DisplayType;
}
