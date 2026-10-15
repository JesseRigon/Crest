using System.ComponentModel.DataAnnotations;
using Grove.Infrastructure.Settings;

namespace Grove.Integrations.Microsoft.Settings;

public class MicrosoftAccountSettings
{
    [Display(Name = "AppId", Description = "The Application ID found on the Microsoft developer portal parameters.", Order = 0)]
    [Required(AllowEmptyStrings = false, ErrorMessage = "Application Id is required")]
    public string AppId { get; set; }

    [Display(Name = "Secret Key", Description = "The Application secret key found on the Microsoft developer portal parameters.", Order = 1)]
    [Required(AllowEmptyStrings = false, ErrorMessage = "Application Secret is required")]
    [Setting(Secret = true, ProtectedBy = MicrosoftAuthenticationConstants.Features.MicrosoftAccount)]
    public string AppSecret { get; set; }

    /// <summary>
    /// The callback path; the handler's default when empty.
    /// </summary>
    [Display(Name = "Callback path", Description = "The request path within the application's base path where the user-agent will be returned. The middleware will process this request when it arrives.", Prompt = "/signin-microsoft", Order = 2)]
    [RegularExpression(@"\/[-A-Za-z0-9+&@#\/%?=~_|!:,.;]+[-A-Za-z0-9+&@#\/%=~_|]", ErrorMessage = "Invalid path")]
    public string CallbackPath { get; set; }

    [Display(Name = "Store tokens", Description = "Store external authentication tokens for reuse", Order = 3)]
    public bool SaveTokens { get; set; }
}
