using System.Runtime.Serialization;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Crest.ContentManagement.Display.Models;
using Crest.Flows.Models;

namespace Crest.Flows.ViewModels;

public class FlowPartViewModel
{
    public FlowPart FlowPart { get; set; }

    [IgnoreDataMember]
    [BindNever]
    public BuildPartDisplayContext BuildPartDisplayContext { get; set; }
}
