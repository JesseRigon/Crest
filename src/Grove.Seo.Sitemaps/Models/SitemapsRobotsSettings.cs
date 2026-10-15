using System.ComponentModel;
using System.ComponentModel.DataAnnotations;

namespace Grove.Seo.Sitemaps.Models;

public class SitemapsRobotsSettings
{
    [DefaultValue(true)]
    [Display(Name = "Include all sitemaps.", Description = "Check to let the system include all sitemaps.")]
    public bool IncludeSitemaps { get; set; } = true;
}
