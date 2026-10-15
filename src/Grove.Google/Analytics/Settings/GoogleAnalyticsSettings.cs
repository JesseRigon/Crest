using System.ComponentModel.DataAnnotations;

namespace Grove.Google.Analytics.Settings;

public class GoogleAnalyticsSettings
{
    [Display(Name = "Tracking ID", Description = "The tracking ID defined in your Google Analytics account.")]
    [Required(AllowEmptyStrings = false)]
    public string TrackingID { get; set; }
}
