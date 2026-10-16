using System.Globalization;
using System.Net.Http;
using Azure;
using Grove.Data.Connectors;
using Grove.Infrastructure.Settings;

namespace Grove.Integrations.Microsoft;

/// <summary>
/// Azure Communication Services: the e-mail and SMS features' ids, their declared connections
/// (the <c>sdk</c> protocol; the tenant credential is the resource's connection string) and how
/// a failed SDK call is classified for the engine.
/// </summary>
public static class AzureCommunicationServices
{
    public static class Features
    {
        public const string Email = "Grove.Integrations.Microsoft.Email";

        public const string Sms = "Grove.Integrations.Microsoft.Sms";
    }

    public const string EmailConnectionKey = "azure-communication-email";

    public const string SmsConnectionKey = "azure-communication-sms";

    /// <summary>Declares the e-mail connection.</summary>
    public static void DeclareEmail(ConnectionBuilder connection) => Declare(
        connection,
        "Azure Communication Services Email",
        "Sends e-mail through an Azure Communication Services resource; the credential is its connection string.",
        Features.Email,
        new ConnectionOperation { Id = "send-email", DisplayName = "Send an email", Kind = OperationKinds.Sdk, IsSafeToRetry = false, Tags = ["Email"] });

    /// <summary>Declares the SMS connection.</summary>
    public static void DeclareSms(ConnectionBuilder connection) => Declare(
        connection,
        "Azure Communication Services SMS",
        "Sends SMS through an Azure Communication Services resource; the credential is its connection string.",
        Features.Sms,
        new ConnectionOperation { Id = "send-sms", DisplayName = "Send an SMS", Kind = OperationKinds.Sdk, IsSafeToRetry = false, Tags = ["SMS"] });

    private static void Declare(ConnectionBuilder connection, string displayName, string description, string featureId, ConnectionOperation operation) => connection
        .DisplayName(displayName, description)
        .Protocol(ConnectorProtocols.Sdk)
        .Feature(featureId)
        .Auth(new ConnectionAuth { Scheme = AuthSchemes.Custom })
        .AllowScopes(CredentialScope.Tenant)
        .Policy(policy => policy with { RetryCount = 2 })
        .Operation(operation);

    /// <summary>
    /// A failed SDK call. A request the service answered with 4xx was refused, so nothing was
    /// sent (429 asks to slow down); a request that never reached it (DNS, connect, TLS) was not
    /// sent either. A 5xx or a broken exchange leaves it unknown.
    /// </summary>
    public static FailureClassification Classify(Exception exception) => exception switch
    {
        ConnectorRefusedException => new FailureClassification(FailureKind.Refused, NotSent: true),
        CredentialUnreadableException => new FailureClassification(FailureKind.Unauthorized, NotSent: true),
        RequestFailedException { Status: 401 or 403 } => new FailureClassification(FailureKind.Unauthorized, NotSent: true),
        RequestFailedException { Status: 429 } limited => new FailureClassification(FailureKind.RateLimited, RetryAfter(limited), NotSent: true),
        RequestFailedException { Status: 408 } => new FailureClassification(FailureKind.Transient),
        RequestFailedException { Status: >= 400 and < 500 } => new FailureClassification(FailureKind.Permanent, NotSent: true),
        RequestFailedException { Status: >= 500 } => new FailureClassification(FailureKind.Transient),
        RequestFailedException { InnerException: HttpRequestException http } => Classify(http),
        HttpRequestException { HttpRequestError: HttpRequestError.NameResolutionError or HttpRequestError.ConnectionError or HttpRequestError.SecureConnectionError }
            => new FailureClassification(FailureKind.Transient, NotSent: true),
        HttpRequestException or TimeoutException or IOException => new FailureClassification(FailureKind.Transient),
        _ => new FailureClassification(FailureKind.Permanent),
    };

    private static TimeSpan? RetryAfter(RequestFailedException exception)
    {
        var response = exception.GetRawResponse();
        if (response is not null
            && response.Headers.TryGetValue("Retry-After", out var value)
            && int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var seconds))
        {
            return TimeSpan.FromSeconds(seconds);
        }

        return null;
    }
}

/// <summary>The connections of one Azure Communication Services feature: a provider's connection choices.</summary>
public abstract class AzureCommunicationConnectionOptions(IConnectionRegistry registry, string featureId) : ISettingOptions
{
    public async Task<IReadOnlyList<SettingOption>> GetOptionsAsync(CancellationToken cancellationToken = default) =>
        [.. (await registry.ListAsync(cancellationToken))
            .Where(connection => connection.FeatureId == featureId)
            .OrderBy(connection => connection.DisplayName, StringComparer.CurrentCultureIgnoreCase)
            .Select(connection => new SettingOption(connection.Key, connection.DisplayName))];
}

public sealed class AzureEmailConnectionOptions(IConnectionRegistry registry)
    : AzureCommunicationConnectionOptions(registry, AzureCommunicationServices.Features.Email);

public sealed class AzureSmsConnectionOptions(IConnectionRegistry registry)
    : AzureCommunicationConnectionOptions(registry, AzureCommunicationServices.Features.Sms);
