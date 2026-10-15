using Microsoft.AspNetCore.Mvc.ModelBinding;
using Grove.ContentManagement;

namespace Grove.Flows.Models;

public class BagPart : ContentPart
{
    [BindNever]
    public List<ContentItem> ContentItems { get; set; } = [];
}
