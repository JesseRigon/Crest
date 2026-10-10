using System.Runtime.Serialization;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Crest.DisplayManagement.ModelBinding;
using Crest.Widgets.Models;

namespace Crest.Widgets.ViewModels;

public class WidgetsListPartEditViewModel
{
    public string[] AvailableZones { get; set; } = [];

    public string[] Zones { get; set; } = [];
    public string[] Prefixes { get; set; } = [];
    public string[] ContentTypes { get; set; } = [];
    public string[] ContentItems { get; set; } = [];

    public WidgetsListPart WidgetsListPart { get; set; }

    [IgnoreDataMember]
    [BindNever]
    public IUpdateModel Updater { get; set; }
}
