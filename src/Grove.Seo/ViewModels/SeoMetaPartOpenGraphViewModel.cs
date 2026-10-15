using Microsoft.AspNetCore.Mvc.ModelBinding;
using Grove.Seo.Models;

namespace Grove.Seo.ViewModels;

public class SeoMetaPartOpenGraphViewModel
{
    public string OpenGraphType { get; set; }
    public string OpenGraphTitle { get; set; }
    public string OpenGraphDescription { get; set; }

    [BindNever]
    public SeoMetaPart SeoMetaPart { get; set; }
}
