using Grove.Access;
using Grove.Communications.Delivery;
using Grove.Communications.Email;
using Grove.Data.Connectors;
using Grove.Infrastructure;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Grove.Data.Protocols.Smtp.Services;

/// <summary>
/// The tenant's SMTP provider: turns the message into an SMTP send on the picked connection
/// (<see cref="SmtpOptions.ConnectionKey"/>) and asks the connector engine to run it as the
/// tenant's system actor. It holds no credential and retries nothing itself: the engine does,
/// only when the protocol proves the message was not handed over.
/// </summary>
public sealed class SmtpEmailProvider : IEmailProvider, IConnectedProvider
{
    public const string TechnicalName = "SMTP";

    private readonly IOptionsMonitor<SmtpOptions> _options;
    private readonly IConnectorEngine _engine;
    private readonly ICallerContextFactory _callers;
    private readonly IEmailAddressValidator _emailAddressValidator;
    private readonly ILogger _logger;

    private readonly IStringLocalizer S;

    public SmtpEmailProvider(
        IOptionsMonitor<SmtpOptions> options,
        IConnectorEngine engine,
        ICallerContextFactory callers,
        IEmailAddressValidator emailAddressValidator,
        ILogger<SmtpEmailProvider> logger,
        IStringLocalizer<SmtpEmailProvider> stringLocalizer)
    {
        _options = options;
        _engine = engine;
        _callers = callers;
        _emailAddressValidator = emailAddressValidator;
        _logger = logger;
        S = stringLocalizer;
    }

    public LocalizedString DisplayName => S["Simple Mail Transfer Protocol (SMTP)"];

    public string ConnectionKey => _options.CurrentValue.ConnectionKey;

    public async Task<Result> SendAsync(MailMessage message, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(message);

        var options = _options.CurrentValue;
        if (!options.IsEnabled)
        {
            return Result.Failed(S["The SMTP Email Provider is disabled."]);
        }

        if (string.IsNullOrEmpty(options.ConnectionKey))
        {
            return Result.Failed(S["The SMTP Email Provider has no connection."]);
        }

        var mime = SmtpMessages.Create(message, options.DefaultSender, _emailAddressValidator, S);
        if (!mime.Succeeded)
        {
            return mime;
        }

        var caller = await _callers.CreateSystemAsync(cancellationToken: cancellationToken);
        var result = await _engine.ExecuteAsync(new ConnectorCall
        {
            ConnectionKey = options.ConnectionKey,
            Caller = caller,
            Request = new ConnectorRequest(
                SmtpConnectorProtocol.SendMethod,
                null,
                await SmtpMessages.SerializeAsync(mime.Value, cancellationToken),
                SmtpConnectorProtocol.MessageContentType),
        }, cancellationToken);

        if (result.Succeeded)
        {
            return DeliveryResult.From(result, options.ConnectionKey, succeeded: true);
        }

        _logger.LogWarning("The SMTP connection '{Connection}' did not send a message: {Outcome} {Error}", options.ConnectionKey, result.Outcome, result.Error);

        return DeliveryResult.From(result, options.ConnectionKey, succeeded: false, new ResultError
        {
            Message = result.Outcome switch
            {
                ConnectorOutcome.NotAuthorized => S["The SMTP connection has no usable credential."],
                ConnectorOutcome.RetryAfter => S["The SMTP server asked to try again later."],
                _ => S["An error occurred while sending an email: '{0}'", result.Error ?? result.Outcome.ToString()],
            },
        });
    }
}
