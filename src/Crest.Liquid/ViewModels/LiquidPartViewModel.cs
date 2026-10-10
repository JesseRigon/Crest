using Microsoft.AspNetCore.Mvc.ModelBinding;
using Crest.ContentManagement;
using Crest.Liquid.Models;

namespace Crest.Liquid.ViewModels;

public class LiquidPartViewModel
{
    public string Liquid { get; set; }
    public string Html { get; set; }

    [BindNever]
    public ContentItem ContentItem { get; set; }

    [BindNever]
    public LiquidPart LiquidPart { get; set; }
}
