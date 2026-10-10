using System.ComponentModel.DataAnnotations;

namespace Crest.Google.Analytics.ViewModels;

public class GoogleAnalyticsSettingsViewModel
{
    [Required(AllowEmptyStrings = false)]
    public string TrackingID { get; set; }
}
