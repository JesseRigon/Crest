using Microsoft.AspNetCore.Mvc.ModelBinding;
using Crest.Seo.Models;

namespace Crest.Seo.ViewModels;

public class SeoMetaPartOpenGraphViewModel
{
    public string OpenGraphType { get; set; }
    public string OpenGraphTitle { get; set; }
    public string OpenGraphDescription { get; set; }

    [BindNever]
    public SeoMetaPart SeoMetaPart { get; set; }
}
