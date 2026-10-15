using Microsoft.AspNetCore.Mvc.ModelBinding;
using Grove.Contents.Management;

namespace Grove.Contents.Flows.Models;

public class BagPart : ContentPart
{
    [BindNever]
    public List<ContentItem> ContentItems { get; set; } = [];
}
