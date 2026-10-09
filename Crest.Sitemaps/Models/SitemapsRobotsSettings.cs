using System.ComponentModel;

namespace Crest.Sitemaps.Models;

public class SitemapsRobotsSettings
{
    [DefaultValue(true)]
    public bool IncludeSitemaps { get; set; } = true;
}
