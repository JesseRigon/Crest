using System.ComponentModel.DataAnnotations;

namespace Grove.Integrations.GitHub.Settings;

/// <summary>GitHub sign-in. The client id and secret are the GitHub app credentials on the Connections screen.</summary>
public class GitHubAuthenticationSettings
{
    /// <summary>
    /// The callback path; the handler's default when empty.
    /// </summary>
    [Display(Name = "Callback path", Description = "The request path within the application's base path where the user-agent will be returned. The middleware will process this request when it arrives.", Prompt = "/signin-github", Order = 0)]
    [RegularExpression(@"\/[-A-Za-z0-9+&@#\/%?=~_|!:,.;]+[-A-Za-z0-9+&@#\/%=~_|]", ErrorMessage = "Invalid path")]
    public string CallbackPath { get; set; }

    [Display(Name = "Store tokens", Description = "Store external authentication tokens for reuse", Order = 1)]
    public bool SaveTokens { get; set; }
}
