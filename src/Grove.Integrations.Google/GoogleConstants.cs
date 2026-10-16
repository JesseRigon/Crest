namespace Grove.Integrations.Google;

public static class GoogleConstants
{
    /// <summary>The Google OAuth client's id and secret, shared by Google sign-in and the Google connection.</summary>
    public const string AppCredentialsKey = "google";

    /// <summary>The Google APIs connection.</summary>
    public const string ConnectionKey = "google";

    public static class Features
    {
        public const string Google = "Grove.Integrations.Google";
        public const string GoogleAuthentication = "Grove.Integrations.Google.GoogleAuthentication";
        public const string GoogleAnalytics = "Grove.Integrations.Google.Analytics";
        public const string GoogleTagManager = "Grove.Integrations.Google.TagManager";
    }

    public static class Operations
    {
        public const string GetUserInfo = "getUserInfo";
    }
}
