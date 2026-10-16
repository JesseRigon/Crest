using System.Net.Mail;
using System.Text.Json.Nodes;
using Grove.Access;
using Grove.Communications.Delivery;
using Grove.Communications.Email;
using Grove.Data.Connectors;
using Grove.Infrastructure;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MailMessage = Grove.Communications.Email.MailMessage;

namespace Grove.Integrations.Microsoft.GraphMail;

/// <summary>
/// The Microsoft Graph e-mail provider: turns the message into Graph's <c>sendMail</c> on the
/// Graph connection, run by the engine as the tenant's system actor with the tenant's grant.
/// Graph takes no idempotency key, so the engine retries a send only when nothing was sent.
/// </summary>
public sealed class GraphMailProvider : IEmailProvider, IConnectedProvider
{
    public const string TechnicalName = "MicrosoftGraph";

    private readonly IOptionsMonitor<GraphMailSettings> _settings;
    private readonly IConnectorEngine _engine;
    private readonly ICallerContextFactory _callers;
    private readonly ILogger _logger;
    private readonly IStringLocalizer S;

    public GraphMailProvider(
        IOptionsMonitor<GraphMailSettings> settings,
        IConnectorEngine engine,
        ICallerContextFactory callers,
        ILogger<GraphMailProvider> logger,
        IStringLocalizer<GraphMailProvider> stringLocalizer)
    {
        _settings = settings;
        _engine = engine;
        _callers = callers;
        _logger = logger;
        S = stringLocalizer;
    }

    public LocalizedString DisplayName => S["Microsoft Graph (Exchange Online)"];

    public string ConnectionKey => ConnectionOf(_settings.CurrentValue);

    public async Task<Result> SendAsync(MailMessage message, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(message);

        var settings = _settings.CurrentValue;
        if (!settings.IsEnabled)
        {
            return Result.Failed(S["The Microsoft Graph provider is disabled."]);
        }

        var errors = new List<ResultError>();
        var body = new JsonObject
        {
            ["message"] = ToGraphMessage(message, errors),
            ["saveToSentItems"] = settings.SaveToSentItems,
        };

        if (errors.Count > 0)
        {
            return Result.Failed(errors);
        }

        var connectionKey = ConnectionOf(settings);
        var caller = await _callers.CreateSystemAsync(cancellationToken: cancellationToken);
        var result = await _engine.ExecuteAsync(new ConnectorCall
        {
            ConnectionKey = connectionKey,
            Caller = caller,
            Scope = CredentialScope.Tenant,
            OperationId = MicrosoftAuthenticationConstants.GraphOperations.SendMail,
            Arguments = body,
            Request = new ConnectorRequest("POST", "/me/sendMail", body.ToJsonString(), "application/json"),
        }, cancellationToken);

        if (result.Succeeded)
        {
            return DeliveryResult.From(result, connectionKey, succeeded: true);
        }

        _logger.LogWarning("Microsoft Graph did not send an email: {Outcome} {StatusCode} {Error}", result.Outcome, result.StatusCode, result.Error);

        return DeliveryResult.From(result, connectionKey, succeeded: false, new ResultError
        {
            Message = result.Outcome == ConnectorOutcome.NotAuthorized
                ? S["The Microsoft Graph connection has no tenant grant."]
                : S["An error occurred while sending an email: '{0}'", result.Error ?? result.Outcome.ToString()],
        });
    }

    /// <summary>The Graph message resource for a channel message; invalid addresses become errors.</summary>
    public JsonObject ToGraphMessage(MailMessage message, List<ResultError> errors)
    {
        var recipients = message.GetRecipients();
        var graph = new JsonObject
        {
            ["subject"] = message.Subject ?? string.Empty,
            ["body"] = new JsonObject
            {
                ["contentType"] = string.IsNullOrEmpty(message.HtmlBody) ? "Text" : "HTML",
                ["content"] = string.IsNullOrEmpty(message.HtmlBody) ? message.TextBody ?? string.Empty : message.HtmlBody,
            },
            ["toRecipients"] = Addresses(recipients.To, nameof(message.To), errors),
            ["ccRecipients"] = Addresses(recipients.Cc, nameof(message.Cc), errors),
            ["bccRecipients"] = Addresses(recipients.Bcc, nameof(message.Bcc), errors),
        };

        if (!string.IsNullOrWhiteSpace(message.ReplyTo))
        {
            graph["replyTo"] = Addresses(message.GetReplyTo(), nameof(message.ReplyTo), errors);
        }

        // A From other than the signed-in account needs "send as" rights in Exchange.
        if (!string.IsNullOrWhiteSpace(message.From))
        {
            var from = Addresses(message.GetSender().Take(1), nameof(message.From), errors);
            if (from.Count == 1)
            {
                graph["from"] = from[0]!.DeepClone();
            }
        }

        var attachments = new JsonArray();
        foreach (var attachment in message.Attachments.Where(attachment => attachment.Stream is not null))
        {
            using var content = new MemoryStream();
            attachment.Stream.CopyTo(content);
            attachments.Add(new JsonObject
            {
                ["@odata.type"] = "#microsoft.graph.fileAttachment",
                ["name"] = attachment.Filename,
                ["contentBytes"] = Convert.ToBase64String(content.ToArray()),
            });
        }

        if (attachments.Count > 0)
        {
            graph["attachments"] = attachments;
        }

        return graph;
    }

    private JsonArray Addresses(IEnumerable<string> addresses, string key, List<ResultError> errors)
    {
        var array = new JsonArray();
        foreach (var address in addresses)
        {
            if (MailAddress.TryCreate(address, out var mailAddress))
            {
                var email = new JsonObject { ["address"] = mailAddress.Address };
                if (!string.IsNullOrEmpty(mailAddress.DisplayName))
                {
                    email["name"] = mailAddress.DisplayName;
                }

                array.Add(new JsonObject { ["emailAddress"] = email });
            }
            else
            {
                errors.Add(new ResultError { Key = key, Message = S["Invalid email address: '{0}'.", address] });
            }
        }

        return array;
    }

    private static string ConnectionOf(GraphMailSettings settings) =>
        string.IsNullOrEmpty(settings.ConnectionKey) ? MicrosoftAuthenticationConstants.GraphConnectionKey : settings.ConnectionKey;
}
