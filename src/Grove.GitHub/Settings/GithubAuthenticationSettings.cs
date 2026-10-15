using Microsoft.AspNetCore.Http;

namespace Grove.GitHub.Settings;

public class GitHubAuthenticationSettings
{
    public string ClientID { get; set; }
    public string ClientSecret { get; set; }
    public PathString CallbackPath { get; set; }
    public bool SaveTokens { get; set; }
}
