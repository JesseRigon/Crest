using Microsoft.AspNetCore.Http;

namespace Grove.Facebook.Login.Settings;

public class FacebookLoginSettings
{
    public PathString CallbackPath { get; set; }

    public bool SaveTokens { get; set; }
}
