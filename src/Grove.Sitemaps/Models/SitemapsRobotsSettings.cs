using System.ComponentModel;

namespace Grove.Sitemaps.Models;

public class SitemapsRobotsSettings
{
    [DefaultValue(true)]
    public bool IncludeSitemaps { get; set; } = true;
}
