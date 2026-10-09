using Microsoft.AspNetCore.Mvc.ModelBinding;
using Crest.ContentManagement;

namespace Crest.Flows.Models;

public class FlowPart : ContentPart
{
    [BindNever]
    public List<ContentItem> Widgets { get; set; } = [];
}
