namespace Grove.Integrations.GitHub;

public static class GitHubConstants
{
    /// <summary>The GitHub OAuth app's client id and secret, shared by GitHub sign-in and the GitHub connection.</summary>
    public const string AppCredentialsKey = "github";

    /// <summary>The GitHub REST API connection.</summary>
    public const string ConnectionKey = "github";

    public static class Features
    {
        public const string GitHub = "Grove.Integrations.GitHub";
        public const string GitHubAuthentication = "Grove.Integrations.GitHub.Authentication";
    }

    public static class Operations
    {
        public const string GetAuthenticatedUser = "getAuthenticatedUser";
        public const string ListRepositories = "listRepositories";
    }
}
