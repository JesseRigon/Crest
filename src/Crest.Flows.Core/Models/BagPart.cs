using Microsoft.AspNetCore.Mvc.ModelBinding;
using Crest.ContentManagement;

namespace Crest.Flows.Models;

public class BagPart : ContentPart
{
    [BindNever]
    public List<ContentItem> ContentItems { get; set; } = [];
}
