using Grove.Data.Connectors;

namespace Grove.Integrations.Twilio;

/// <summary>
/// The Twilio connection: Twilio's REST API under <c>https://api.twilio.com</c>, basic auth with
/// a tenant credential (user name: the account SID or an API key SID; password: the auth token
/// or the API key's secret). The SMS provider sends through it; Workflows and Queries run its
/// other operations with the same credential.
/// </summary>
public static class TwilioConnection
{
    public const string FeatureId = "Grove.Integrations.Twilio";

    /// <summary>The declared connection's key.</summary>
    public const string Key = "twilio";

    public const string BaseUrl = "https://api.twilio.com";

    /// <summary>Twilio's idempotency header: a repeated create with the same token is not repeated.</summary>
    public const string IdempotencyHeader = "I-Twilio-Idempotency-Token";

    public const string FormContentType = "application/x-www-form-urlencoded";

    public static readonly ConnectionOperation CreateMessage = new()
    {
        Id = "create-message",
        DisplayName = "Send a message",
        Description = "Creates an outgoing SMS or MMS message.",
        Method = "POST",
        Path = "/2010-04-01/Accounts/{AccountSid}/Messages.json",
        ContentType = FormContentType,
        Parameters =
        [
            new OperationParameter("AccountSid", ParameterLocation.Path, Required: true),
            new OperationParameter("To", ParameterLocation.Body, Required: true),
            new OperationParameter("From", ParameterLocation.Body),
            new OperationParameter("MessagingServiceSid", ParameterLocation.Body),
            new OperationParameter("Body", ParameterLocation.Body),
        ],
        IsSafeToRetry = false,
        SupportsIdempotencyKey = true,
        IdempotencyKeyHeader = IdempotencyHeader,
        Tags = ["Messaging"],
    };

    public static readonly ConnectionOperation ListMessages = new()
    {
        Id = "list-messages",
        DisplayName = "List messages",
        Description = "The account's messages, newest first.",
        Method = "GET",
        Path = "/2010-04-01/Accounts/{AccountSid}/Messages.json",
        Parameters =
        [
            new OperationParameter("AccountSid", ParameterLocation.Path, Required: true),
            new OperationParameter("To", ParameterLocation.Query),
            new OperationParameter("From", ParameterLocation.Query),
            new OperationParameter("DateSent", ParameterLocation.Query),
            new OperationParameter("PageSize", ParameterLocation.Query),
        ],
        IsSafeToRetry = true,
        Paging = new OperationPaging(PagingStyle.Link, Parameter: "PageToken", SizeParameter: "PageSize", NextPath: "next_page_uri"),
        Tags = ["Messaging"],
    };

    public static readonly ConnectionOperation FetchMessage = new()
    {
        Id = "fetch-message",
        DisplayName = "Fetch a message",
        Description = "One message by its SID, with its delivery status.",
        Method = "GET",
        Path = "/2010-04-01/Accounts/{AccountSid}/Messages/{Sid}.json",
        Parameters =
        [
            new OperationParameter("AccountSid", ParameterLocation.Path, Required: true),
            new OperationParameter("Sid", ParameterLocation.Path, Required: true),
        ],
        IsSafeToRetry = true,
        Tags = ["Messaging"],
    };

    public static readonly ConnectionOperation FetchAccount = new()
    {
        Id = "fetch-account",
        DisplayName = "Fetch the account",
        Description = "The account itself: a cheap check that the credential works.",
        Method = "GET",
        Path = "/2010-04-01/Accounts/{AccountSid}.json",
        Parameters = [new OperationParameter("AccountSid", ParameterLocation.Path, Required: true)],
        IsSafeToRetry = true,
        Tags = ["Account"],
    };

    /// <summary>Declares the connection: REST, basic auth, tenant scope, Twilio's rate limit.</summary>
    public static void Declare(ConnectionBuilder connection) => connection
        .DisplayName("Twilio", "Twilio's REST API: messages and the account.")
        .Protocol(ConnectorProtocols.Rest)
        .Feature(FeatureId)
        .BaseUrl(BaseUrl)
        .Basic()
        .AllowScopes(CredentialScope.Tenant)
        .Policy(policy => policy with
        {
            RetryCount = 2,
            RateLimit = new RateLimitPolicy(100, TimeSpan.FromSeconds(1)),
        })
        .Operation(CreateMessage)
        .Operation(ListMessages)
        .Operation(FetchMessage)
        .Operation(FetchAccount);
}
