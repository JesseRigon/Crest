using Microsoft.AspNetCore.Mvc.ModelBinding;
using Grove.Sitemaps.Models;

namespace Grove.Sitemaps.ViewModels;

public class EditSourceViewModel
{
    public string SitemapId { get; set; }
    public string SitemapSourceId { get; set; }
    public dynamic Editor { get; set; }

    [BindNever]
    public SitemapSource SitemapSource { get; set; }

}
