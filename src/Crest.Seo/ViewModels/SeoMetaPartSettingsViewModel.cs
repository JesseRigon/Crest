using Crest.Seo.Models;

namespace Crest.Seo.ViewModels;

public class SeoMetaPartSettingsViewModel
{
    public bool DisplayKeywords { get; set; }
    public bool DisplayCustomMetaTags { get; set; }
    public bool DisplayOpenGraph { get; set; }
    public bool DisplayTwitter { get; set; }
    public bool DisplayGoogleSchema { get; set; }
    public SeoMetaPartSettings SeoMetaPartSettings { get; set; }
}
