using System.Text.Json;
using System.Text.Json.Nodes;
using Grove.Access;
using Grove.Communications.Delivery;
using Grove.Communications.Sms;
using Grove.Data.Connectors;
using Grove.Infrastructure;
using Grove.Integrations.Twilio.Models;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Grove.Integrations.Twilio.Services;

/// <summary>
/// The Twilio SMS provider: turns the message into Twilio's "create message" operation on the
/// picked connection and asks the connector engine to run it as the tenant's system actor. It
/// holds no credential and retries nothing itself; each send carries an idempotency token, so
/// the engine may repeat it without sending twice.
/// </summary>
public sealed class TwilioSmsProvider : ISmsProvider, IConnectedProvider
{
    public const string TechnicalName = "Twilio";

    private static readonly JsonSerializerOptions s_json = new() { PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower };

    private static readonly HashSet<string> s_acceptedStatuses = new(StringComparer.OrdinalIgnoreCase)
    {
        "accepted", "scheduled", "queued", "sending", "sent", "delivered",
    };

    private readonly IOptionsMonitor<TwilioOptions> _options;
    private readonly IConnectorEngine _engine;
    private readonly ICallerContextFactory _callers;
    private readonly ILogger _logger;

    private readonly IStringLocalizer S;

    public TwilioSmsProvider(
        IOptionsMonitor<TwilioOptions> options,
        IConnectorEngine engine,
        ICallerContextFactory callers,
        ILogger<TwilioSmsProvider> logger,
        IStringLocalizer<TwilioSmsProvider> stringLocalizer)
    {
        _options = options;
        _engine = engine;
        _callers = callers;
        _logger = logger;
        S = stringLocalizer;
    }

    public LocalizedString Name => S["Twilio"];

    public string ConnectionKey => ConnectionOf(_options.CurrentValue);

    private static string ConnectionOf(TwilioOptions options) =>
        string.IsNullOrEmpty(options.ConnectionKey) ? TwilioConnection.Key : options.ConnectionKey;

    public async Task<Result> SendAsync(SmsMessage message, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(message);

        if (string.IsNullOrEmpty(message.To))
        {
            return Result.Failed(new ResultError { Key = nameof(message.To), Message = S["A phone number is required for the recipient."] });
        }

        if (string.IsNullOrEmpty(message.Body))
        {
            return Result.Failed(new ResultError { Key = nameof(message.Body), Message = S["The message body is required."] });
        }

        var options = _options.CurrentValue;
        if (!options.IsEnabled || string.IsNullOrEmpty(options.AccountSID))
        {
            return Result.Failed(S["The Twilio provider is disabled or has no account SID."]);
        }

        var from = string.IsNullOrEmpty(message.From) ? options.PhoneNumber : message.From;
        var form = await new FormUrlEncodedContent(
        [
            new("From", from),
            new("To", message.To),
            new("Body", message.Body),
        ]).ReadAsStringAsync(cancellationToken);

        var caller = await _callers.CreateSystemAsync(cancellationToken: cancellationToken);
        var result = await _engine.ExecuteAsync(new ConnectorCall
        {
            ConnectionKey = ConnectionOf(options),
            Caller = caller,
            OperationId = TwilioConnection.CreateMessage.Id,
            Arguments = new JsonObject
            {
                ["AccountSid"] = options.AccountSID,
                ["From"] = from,
                ["To"] = message.To,
                ["Body"] = message.Body,
            },
            Request = new ConnectorRequest(
                "POST",
                $"/2010-04-01/Accounts/{Uri.EscapeDataString(options.AccountSID)}/Messages.json",
                form,
                TwilioConnection.FormContentType),
            IdempotencyKey = Guid.NewGuid().ToString("N"),
        }, cancellationToken);

        var response = Read(result);
        if (result.Succeeded && response?.Status is { } status && s_acceptedStatuses.Contains(status))
        {
            return DeliveryResult.From(result, ConnectionOf(options), succeeded: true);
        }

        _logger.LogError(
            "Twilio did not accept an SMS message: {Outcome} {StatusCode}, code {Code}: {Message}",
            result.Outcome,
            result.StatusCode,
            response?.ErrorCode ?? response?.Code,
            response?.ErrorMessage ?? response?.Message ?? result.Error);

        return DeliveryResult.From(result, ConnectionOf(options), succeeded: false, new ResultError
        {
            Message = result.Outcome switch
            {
                ConnectorOutcome.NotAuthorized => S["The Twilio connection has no usable credential."],
                ConnectorOutcome.RetryAfter => S["Twilio asked to try again later."],
                _ => S["The SMS message has not been sent."],
            },
        });
    }

    private static TwilioMessageResponse Read(ConnectorResult result)
    {
        try
        {
            if (result.Value is JsonObject value)
            {
                return value.Deserialize<TwilioMessageResponse>(s_json);
            }

            return string.IsNullOrEmpty(result.Body) ? null : JsonSerializer.Deserialize<TwilioMessageResponse>(result.Body, s_json);
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
