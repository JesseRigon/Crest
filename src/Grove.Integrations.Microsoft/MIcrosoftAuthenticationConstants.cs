namespace Grove.Integrations.Microsoft;

public static class MicrosoftAuthenticationConstants
{
    /// <summary>The Microsoft Graph connection.</summary>
    public const string GraphConnectionKey = "microsoft-graph";

    /// <summary>
    /// The Microsoft identity platform app registration's client id and secret, shared by
    /// Microsoft account sign-in, Entra ID sign-in and the Microsoft Graph connection. Keyed by
    /// the connection, so the connection's tenant credential can hold them too.
    /// </summary>
    public const string AppCredentialsKey = GraphConnectionKey;

    public static class Features
    {
        public const string MicrosoftAccount = "Grove.Integrations.Microsoft.MicrosoftAccount";
        public const string AAD = "Grove.Integrations.Microsoft.AzureAD";
        public const string Graph = "Grove.Integrations.Microsoft.Graph";

        /// <summary>The e-mail send provider over the Graph connection.</summary>
        public const string GraphMail = "Grove.Integrations.Microsoft.GraphMail";

        /// <summary>The e-mail receive provider reading the Graph connection's mailbox.</summary>
        public const string GraphMailbox = "Grove.Integrations.Microsoft.GraphMailbox";
    }

    public static class GraphOperations
    {
        public const string GetMe = "getMe";
        public const string ListUsers = "listUsers";
        public const string SendMail = "sendMail";
        public const string ListMessages = "listMessages";
        public const string GetMessageMime = "getMessageMime";
        public const string DeleteMessage = "deleteMessage";
    }
}
