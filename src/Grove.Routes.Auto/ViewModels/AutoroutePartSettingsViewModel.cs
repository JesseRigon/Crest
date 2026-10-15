using Grove.Routes.Auto.Models;

namespace Grove.Routes.Auto.ViewModels;

public class AutoroutePartSettingsViewModel
{
    public bool AllowCustomPath { get; set; }
    public string Pattern { get; set; }
    public bool ShowHomepageOption { get; set; }
    public bool AllowUpdatePath { get; set; }
    public bool AllowDisabled { get; set; }
    public bool AllowRouteContainedItems { get; set; }
    public bool ManageContainedItemRoutes { get; set; }
    public bool AllowAbsolutePath { get; set; }
    public AutoroutePartSettings AutoroutePartSettings { get; set; }
}
