using System.ComponentModel.DataAnnotations;
using Grove.Settings;

namespace Grove.Google.Authentication.Settings;

public class GoogleAuthenticationSettings
{
    [Display(Name = "Client ID", Description = "The client id defined in Google API console for the application.", Order = 0)]
    [Required(AllowEmptyStrings = false, ErrorMessage = "ClientID key is required")]
    public string ClientID { get; set; }

    [Display(Name = "API secret key", Description = "The client secret defined in Google API console for the application.", Order = 1)]
    [Required(AllowEmptyStrings = false, ErrorMessage = "ClientSecret is required")]
    [Setting(Secret = true, ProtectedBy = GoogleConstants.Features.GoogleAuthentication)]
    public string ClientSecret { get; set; }

    /// <summary>
    /// The callback path; the handler's default when empty.
    /// </summary>
    [Display(Name = "Callback path", Description = "The request path within the application's base path where the user-agent will be returned. The middleware will process this request when it arrives.", Prompt = "/signin-google", Order = 2)]
    [RegularExpression(@"\/[-A-Za-z0-9+&@#\/%?=~_|!:,.;]+[-A-Za-z0-9+&@#\/%=~_|]", ErrorMessage = "Invalid path")]
    public string CallbackPath { get; set; }

    [Display(Name = "Store tokens", Description = "Store external authentication tokens for reuse", Order = 3)]
    public bool SaveTokens { get; set; }
}
