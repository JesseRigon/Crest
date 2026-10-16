using Microsoft.AspNetCore.Mvc.ModelBinding;
using Grove.Contents.Management;

namespace Grove.Contents.BagParts.Models;

public class BagPart : ContentPart
{
    [BindNever]
    public List<ContentItem> ContentItems { get; set; } = [];
}
