using Grove.Security.Permissions;

namespace Grove.Integrations.Facebook;

public static class FacebookConstants
{
    public static readonly Permission ManageFacebookPixelPermission
        = new("ManageFacebookPixel", "Manage Facebook Pixel settings.");

    public const string PixelSettingsGroupId = "facebook-pixel";

    /// <summary>The data-protection purpose the Conversions API token was sealed under in the pixel settings (read once, by the migration that moves it).</summary>
    public const string ConversionsApiProtectorName = "Grove.Integrations.Facebook.ConversionsApi";

    /// <summary>The Graph API connection (OAuth 2.0 on the Meta app credentials).</summary>
    public const string GraphConnectionKey = "facebook-graph";

    /// <summary>
    /// The Meta app's id and secret, shared by Meta Login and the Graph API connection. Keyed by
    /// the connection, so the connection's tenant credential can hold them too.
    /// </summary>
    public const string AppCredentialsKey = GraphConnectionKey;

    /// <summary>The Conversions API connection: the dataset's access token as an API key.</summary>
    public const string ConversionsConnectionKey = "meta-conversions";

    /// <summary>The Graph API version the connections call (https://developers.facebook.com/docs/graph-api/changelog; bump periodically).</summary>
    public const string GraphApiVersion = "v21.0";

    public static class Operations
    {
        public const string GetMe = "getMe";
        public const string SendEvents = "sendEvents";
    }

    public static class Features
    {
        public const string Login = "Grove.Integrations.Facebook.Login";
        public const string Core = "Grove.Integrations.Facebook";
        public const string Pixel = "Grove.Integrations.Facebook.Pixel";
    }
}
