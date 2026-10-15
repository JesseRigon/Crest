using System.ComponentModel.DataAnnotations;
using Grove.Settings;

namespace Grove.Twitter.Settings;

public class TwitterSettings
{
    [Display(Name = "API key", Description = "The API key defined in the Keys and tokens tab of X (Twitter) app dashboard.", Order = 0)]
    [Required(AllowEmptyStrings = false, ErrorMessage = "API key is required")]
    public string ConsumerKey { get; set; }

    [Display(Name = "API secret key", Description = "The API secret defined in the Keys and tokens tab of X (Twitter) app dashboard.", Order = 1)]
    [Required(AllowEmptyStrings = false, ErrorMessage = "API secret key is required")]
    [Setting(Secret = true, ProtectedBy = TwitterConstants.Features.Twitter)]
    public string ConsumerSecret { get; set; }

    [Display(Name = "Access Token", Description = "The Access Token defined in the Keys and tokens tab of X (Twitter) app dashboard.", Order = 2)]
    [Required(AllowEmptyStrings = false, ErrorMessage = "Access token is required")]
    public string AccessToken { get; set; }

    [Display(Name = "Access Token Secret", Description = "The Access Token Secret in the Keys and tokens tab of X (Twitter) app dashboard.", Order = 3)]
    [Required(AllowEmptyStrings = false, ErrorMessage = "Access token secret is required")]
    [Setting(Secret = true, ProtectedBy = TwitterConstants.Features.Twitter)]
    public string AccessTokenSecret { get; set; }
}
