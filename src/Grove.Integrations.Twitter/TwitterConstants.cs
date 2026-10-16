namespace Grove.Integrations.Twitter;

public static class TwitterConstants
{
    /// <summary>The connection to the X API v2 (posting, the signed-in user).</summary>
    public const string ConnectionKey = "x";

    /// <summary>The X app's OAuth 2.0 client id and secret, shared by Sign in with X and the connection.</summary>
    public const string AppCredentialsKey = "x";

    /// <summary>The authentication scheme; kept from the OAuth 1.0a handler so existing external logins still match.</summary>
    public const string AuthenticationScheme = "Twitter";

    public static class Features
    {
        public const string Twitter = "Grove.Integrations.Twitter";
        public const string Signin = "Grove.Integrations.Twitter.Signin";
    }

    /// <summary>The operations the connection declares.</summary>
    public static class Operations
    {
        public const string CreatePost = "createPost";
        public const string GetMe = "getMe";
    }

    /// <summary>X's OAuth 2.0 endpoints.</summary>
    public static class Endpoints
    {
        public const string Api = "https://api.x.com/2";
        public const string Authorize = "https://x.com/i/oauth2/authorize";
        public const string Token = "https://api.x.com/2/oauth2/token";
        public const string Revoke = "https://api.x.com/2/oauth2/revoke";
        public const string Me = "https://api.x.com/2/users/me";
    }
}
