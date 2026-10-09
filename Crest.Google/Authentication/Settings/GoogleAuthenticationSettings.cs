using Microsoft.AspNetCore.Http;

namespace Crest.Google.Authentication.Settings;

public class GoogleAuthenticationSettings
{
    public string ClientID { get; set; }
    public string ClientSecret { get; set; }
    public PathString CallbackPath { get; set; }
    public bool SaveTokens { get; set; }
}
