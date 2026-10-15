using System.ComponentModel.DataAnnotations;
using Grove.Settings;

namespace Grove.GitHub.Settings;

public class GitHubAuthenticationSettings
{
    [Display(Name = "Client ID", Description = "The Client ID defined in the general tab of your GitHub App (https://github.com/settings/apps).", Order = 0)]
    [Required(AllowEmptyStrings = false, ErrorMessage = "API key is required")]
    public string ClientID { get; set; }

    [Display(Name = "Client secret", Description = "The secret for your app.", Order = 1)]
    [Required(AllowEmptyStrings = false, ErrorMessage = "API secret key is required")]
    [Setting(Secret = true, ProtectedBy = GitHubConstants.Features.GitHubAuthentication)]
    public string ClientSecret { get; set; }

    /// <summary>
    /// The callback path; the handler's default when empty.
    /// </summary>
    [Display(Name = "Callback path", Description = "The request path within the application's base path where the user-agent will be returned. The middleware will process this request when it arrives.", Prompt = "/signin-github", Order = 2)]
    [RegularExpression(@"\/[-A-Za-z0-9+&@#\/%?=~_|!:,.;]+[-A-Za-z0-9+&@#\/%=~_|]", ErrorMessage = "Invalid path")]
    public string CallbackPath { get; set; }

    [Display(Name = "Store tokens", Description = "Store external authentication tokens for reuse", Order = 3)]
    public bool SaveTokens { get; set; }
}
