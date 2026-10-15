using Microsoft.AspNetCore.Mvc.ModelBinding;
using Grove.Sitemaps.Models;

namespace Grove.Sitemaps.ViewModels;

public class CreateSourceViewModel
{
    public string SitemapId { get; set; }
    public string SitemapSourceId { get; set; }
    public string SitemapSourceType { get; set; }
    public dynamic Editor { get; set; }

    [BindNever]
    public SitemapSource SitemapSource { get; set; }

}
