using Grove.Themes.Models;

namespace Grove.Themes.ViewModels;

public class SelectThemesViewModel
{
    public string SiteThemeName { get; set; }
    public string AdminThemeName { get; set; }
    public IEnumerable<ThemeEntry> Themes { get; set; }
}
