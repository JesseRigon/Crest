using System.Runtime.Serialization;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Grove.Contents.Management;
using Grove.Contents.Management.Display.Models;
using Grove.Contents.BagParts.Models;

namespace Grove.Contents.BagParts.ViewModels;

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
