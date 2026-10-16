using Azure.Communication.Sms;
using Azure.Core.Pipeline;
using Grove.Access;
using Grove.Communications.Delivery;
using Grove.Communications.Sms;
using Grove.Data.Connectors;
using Grove.Infrastructure;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Grove.Integrations.Microsoft.Sms;

/// <summary>
/// The Azure Communication Services SMS provider: runs the SDK's send inside the connector
/// engine (<see cref="IConnectorEngine.RunSdkAsync{T}"/>) on the picked connection, whose tenant
/// credential is the resource's connection string. The SDK's own retries are off.
/// </summary>
public sealed class AzureSmsProvider : ISmsProvider, IConnectedProvider
{
    public const string TechnicalName = "Azure";

    private readonly IOptionsMonitor<AzureSmsOptions> _options;
    private readonly IConnectorEngine _engine;
    private readonly ICallerContextFactory _callers;
    private readonly IPhoneFormatValidator _phoneFormatValidator;
    private readonly ILogger _logger;

    private readonly IStringLocalizer S;

    public AzureSmsProvider(
        IOptionsMonitor<AzureSmsOptions> options,
        IConnectorEngine engine,
        ICallerContextFactory callers,
        IPhoneFormatValidator phoneFormatValidator,
        ILogger<AzureSmsProvider> logger,
        IStringLocalizer<AzureSmsProvider> stringLocalizer)
    {
        _options = options;
        _engine = engine;
        _callers = callers;
        _phoneFormatValidator = phoneFormatValidator;
        _logger = logger;
        S = stringLocalizer;
    }

    public LocalizedString Name => S["Azure Communication Services"];

    public string ConnectionKey => ConnectionOf(_options.CurrentValue);

    private static string ConnectionOf(AzureSmsOptions options) =>
        string.IsNullOrEmpty(options.ConnectionKey) ? AzureCommunicationServices.SmsConnectionKey : options.ConnectionKey;

    public async Task<Result> SendAsync(SmsMessage message, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(message);

        var options = _options.CurrentValue;
        if (!options.IsEnabled)
        {
            return Result.Failed(S["The Azure Communication Provider is disabled."]);
        }

        if (string.IsNullOrWhiteSpace(message.To))
        {
            return Result.Failed(new ResultError { Key = nameof(message.To), Message = S["A phone number is required for the recipient."] });
        }

        if (!_phoneFormatValidator.IsValid(message.To))
        {
            return Result.Failed(new ResultError { Key = nameof(message.To), Message = S["Invalid phone number format for the recipient: '{0}'.", message.To] });
        }

        if (string.IsNullOrEmpty(message.Body))
        {
            return Result.Failed(new ResultError { Key = nameof(message.Body), Message = S["The message body is required."] });
        }

        var from = string.IsNullOrEmpty(message.From) ? options.PhoneNumber : message.From;
        var caller = await _callers.CreateSystemAsync(cancellationToken: cancellationToken);
        var result = await _engine.RunSdkAsync(
            ConnectionOf(options),
            caller,
            async (context, token) =>
            {
                var connectionString = context.Credential?[CredentialKeys.ConnectionString]
                    ?? throw new ConnectorRefusedException("The Azure Communication Services connection has no connection string.");
                var clientOptions = new SmsClientOptions { Transport = new HttpClientTransport(context.HttpClient) };
                clientOptions.Retry.MaxRetries = 0;

                var response = await new SmsClient(connectionString, clientOptions).SendAsync(from, message.To, message.Body, options: default, token);
                return response.Value;
            },
            new SdkCallOptions { IsSafeToRetry = false, Classify = AzureCommunicationServices.Classify },
            cancellationToken);

        if (result.Succeeded && result.Result?.Successful == true)
        {
            return DeliveryResult.From(result, ConnectionOf(options), succeeded: true);
        }

        // Never the SDK's message: it may carry the connection string.
        _logger.LogError(
            "Azure Communication Services did not send an SMS: {Outcome} {StatusCode} {Failure} {SmsStatus}",
            result.Outcome,
            result.StatusCode,
            result.Failure,
            result.Result?.HttpStatusCode);

        return DeliveryResult.From(result, ConnectionOf(options), succeeded: false, new ResultError
        {
            Message = result.Outcome == ConnectorOutcome.NotAuthorized
                ? S["The Azure Communication Services connection has no usable credential."]
                : S["The SMS message has not been sent."],
        });
    }
}
