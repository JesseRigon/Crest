using System.Globalization;
using System.Text;
using System.Text.Json.Nodes;
using Grove.Access;
using Grove.Communications.Delivery;
using Grove.Communications.Email;
using Grove.Data.Connectors;
using Grove.Infrastructure;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Options;
using MimeKit;

namespace Grove.Integrations.Microsoft.GraphMail;

/// <summary>
/// Reads a mailbox through Microsoft Graph: lists a folder's messages received after the
/// cursor (the last message's received time), oldest first, then reads each one's MIME. The
/// account is the one the Graph connection's tenant grant signed in with.
/// </summary>
public sealed class GraphMailboxReceiveProvider : IEmailReceiveProvider, IConnectedProvider
{
    public const string TechnicalName = "MicrosoftGraphMailbox";

    public const string DefaultFolder = "inbox";

    private readonly IOptionsMonitor<GraphMailboxSettings> _settings;
    private readonly IConnectorEngine _engine;
    private readonly ICallerContextFactory _callers;
    private readonly IStringLocalizer S;

    public GraphMailboxReceiveProvider(
        IOptionsMonitor<GraphMailboxSettings> settings,
        IConnectorEngine engine,
        ICallerContextFactory callers,
        IStringLocalizer<GraphMailboxReceiveProvider> stringLocalizer)
    {
        _settings = settings;
        _engine = engine;
        _callers = callers;
        S = stringLocalizer;
    }

    public LocalizedString DisplayName => S["Microsoft Graph mailbox (Exchange Online)"];

    public string ConnectionKey => ConnectionOf(_settings.CurrentValue);

    public async Task<Result<EmailReceiveBatch>> ReceiveAsync(EmailReceiveRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var settings = _settings.CurrentValue;
        if (!settings.IsEnabled)
        {
            return Result.Failed<EmailReceiveBatch>(S["The Microsoft Graph mailbox is disabled."]);
        }

        var folder = request.Folder ?? (string.IsNullOrWhiteSpace(settings.Folder) ? DefaultFolder : settings.Folder);
        var max = Math.Clamp(request.MaxMessages, 1, 100);
        var connectionKey = ConnectionOf(settings);
        var caller = await _callers.CreateSystemAsync(cancellationToken: cancellationToken);

        var (cursorTime, seen) = MailboxCursor.Parse(request.Cursor);

        // "ge", not "gt": messages that share the cursor's time are read again and the ones the
        // cursor names as seen are skipped, so none is lost. Ask for enough to get past them.
        var query = new Dictionary<string, string>
        {
            ["$select"] = "id,receivedDateTime",
            ["$orderby"] = "receivedDateTime asc",
            ["$top"] = Math.Min(max + seen.Count, 1000).ToString(CultureInfo.InvariantCulture),
        };

        if (cursorTime is not null)
        {
            query["$filter"] = $"receivedDateTime ge {cursorTime}";
        }

        var arguments = new JsonObject { ["folder"] = folder };
        foreach (var (name, value) in query)
        {
            arguments[name] = value;
        }

        var path = $"/me/mailFolders/{Uri.EscapeDataString(folder)}/messages?"
            + string.Join("&", query.Select(entry => $"{entry.Key}={Uri.EscapeDataString(entry.Value)}"));

        var list = await _engine.ExecuteAsync(new ConnectorCall
        {
            ConnectionKey = connectionKey,
            Caller = caller,
            Scope = CredentialScope.Tenant,
            OperationId = MicrosoftAuthenticationConstants.GraphOperations.ListMessages,
            Arguments = arguments,
            Request = new ConnectorRequest("GET", path),
        }, cancellationToken);

        if (!list.Succeeded)
        {
            return Result.Failed<EmailReceiveBatch>(Describe(list));
        }

        // Oldest first, then by id: a stable order inside one timestamp.
        var listed = (list.Value?["value"] as JsonArray ?? [])
            .OfType<JsonObject>()
            .Select(item => (Id: item["id"]?.GetValue<string>(), Time: item["receivedDateTime"]?.GetValue<string>()))
            .Where(item => item.Id is not null && item.Time is not null)
            .OrderBy(item => DateTimeOffset.Parse(item.Time!, CultureInfo.InvariantCulture))
            .ThenBy(item => item.Id, StringComparer.Ordinal)
            .ToList();

        var messages = new List<ReceivedMailMessage>();
        var cursor = new MailboxCursor(cursorTime, seen);
        var unseen = listed.Count(item => !cursor.Has(item.Time!, item.Id!));
        foreach (var (id, time) in listed)
        {
            if (cursor.Has(time!, id!))
            {
                continue;
            }

            if (messages.Count == max)
            {
                break;
            }

            var mime = await _engine.ExecuteAsync(new ConnectorCall
            {
                ConnectionKey = connectionKey,
                Caller = caller,
                Scope = CredentialScope.Tenant,
                OperationId = MicrosoftAuthenticationConstants.GraphOperations.GetMessageMime,
                Arguments = new JsonObject { ["id"] = id },
                Request = new ConnectorRequest("GET", $"/me/messages/{Uri.EscapeDataString(id)}/$value"),
            }, cancellationToken);

            if (!mime.Succeeded || string.IsNullOrEmpty(mime.Body))
            {
                // Stop before the gap: the next read starts after the last message returned.
                break;
            }

            using var stream = new MemoryStream(Encoding.UTF8.GetBytes(mime.Body));
            messages.Add(ToReceived(id, await MimeMessage.LoadAsync(stream, cancellationToken)));
            cursor = cursor.After(time!, id!);
        }

        var hasMore = list.Value?["@odata.nextLink"] is not null || messages.Count < unseen;

        return Result.Success(new EmailReceiveBatch(messages, cursor.ToString()) { HasMore = hasMore });
    }

    public async Task<Result> DeleteAsync(IReadOnlyList<string> messageIds, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(messageIds);

        var settings = _settings.CurrentValue;
        if (!settings.IsEnabled)
        {
            return Result.Failed(S["The Microsoft Graph mailbox is disabled."]);
        }

        var caller = await _callers.CreateSystemAsync(cancellationToken: cancellationToken);
        foreach (var id in messageIds)
        {
            var result = await _engine.ExecuteAsync(new ConnectorCall
            {
                ConnectionKey = ConnectionOf(settings),
                Caller = caller,
                Scope = CredentialScope.Tenant,
                OperationId = MicrosoftAuthenticationConstants.GraphOperations.DeleteMessage,
                Arguments = new JsonObject { ["id"] = id },
                Request = new ConnectorRequest("DELETE", $"/me/messages/{Uri.EscapeDataString(id)}"),
            }, cancellationToken);

            // A message already gone is deleted.
            if (!result.Succeeded && result.StatusCode != 404)
            {
                return Result.Failed(Describe(result));
            }
        }

        return Result.Success();
    }

    /// <summary>
    /// Where a Graph mailbox read stopped: the received time of the last message read and the
    /// ids already read at that time (<c>{time}|{id1,id2}</c>). The next read asks for messages
    /// received at or after the time and skips those ids, so messages sharing a timestamp are
    /// neither lost nor read twice.
    /// </summary>
    public sealed record MailboxCursor(string? Time, IReadOnlySet<string> SeenAtTime)
    {
        public static (string? Time, IReadOnlySet<string> Seen) Parse(string? cursor)
        {
            if (string.IsNullOrWhiteSpace(cursor))
            {
                return (null, new HashSet<string>(StringComparer.Ordinal));
            }

            var separator = cursor.IndexOf('|');
            if (separator < 0)
            {
                return (cursor, new HashSet<string>(StringComparer.Ordinal));
            }

            var ids = cursor[(separator + 1)..].Split(',', StringSplitOptions.RemoveEmptyEntries);
            return (cursor[..separator], new HashSet<string>(ids, StringComparer.Ordinal));
        }

        /// <summary>The message was read by an earlier batch.</summary>
        public bool Has(string time, string id) =>
            Time is not null && SameTime(time, Time) && SeenAtTime.Contains(id);

        /// <summary>The cursor after reading a message.</summary>
        public MailboxCursor After(string time, string id) =>
            Time is not null && SameTime(time, Time)
                ? new MailboxCursor(Time, new HashSet<string>(SeenAtTime, StringComparer.Ordinal) { id })
                : new MailboxCursor(time, new HashSet<string>(StringComparer.Ordinal) { id });

        public override string? ToString() =>
            Time is null ? null : $"{Time}|{string.Join(',', SeenAtTime.Order(StringComparer.Ordinal))}";

        private static bool SameTime(string a, string b) =>
            DateTimeOffset.TryParse(a, CultureInfo.InvariantCulture, DateTimeStyles.None, out var left)
            && DateTimeOffset.TryParse(b, CultureInfo.InvariantCulture, DateTimeStyles.None, out var right)
                ? left == right
                : string.Equals(a, b, StringComparison.Ordinal);
    }

    /// <summary>A MIME message as the channel's received message.</summary>
    public static ReceivedMailMessage ToReceived(string id, MimeMessage message)
    {
        var attachments = new List<ReceivedMailAttachment>();
        foreach (var part in message.Attachments.OfType<MimePart>())
        {
            using var content = new MemoryStream();
            part.Content?.DecodeTo(content);
            attachments.Add(new ReceivedMailAttachment(part.FileName ?? "attachment", part.ContentType.MimeType, content.ToArray()));
        }

        return new ReceivedMailMessage
        {
            Id = id,
            MessageId = message.MessageId,
            From = message.From.ToString(),
            To = message.To.ToString(),
            Cc = message.Cc.ToString(),
            ReplyTo = message.ReplyTo.ToString(),
            Subject = message.Subject,
            TextBody = message.TextBody,
            HtmlBody = message.HtmlBody,
            Date = message.Date == DateTimeOffset.MinValue ? null : message.Date,
            Attachments = attachments,
        };
    }

    private LocalizedString Describe(ConnectorResult result) => result.Outcome switch
    {
        ConnectorOutcome.NotAuthorized => S["The Microsoft Graph connection has no tenant grant."],
        ConnectorOutcome.RetryAfter => S["Microsoft Graph asked to try again later."],
        _ => S["The mailbox could not be read: '{0}'", result.Error ?? result.Outcome.ToString()],
    };

    private static string ConnectionOf(GraphMailboxSettings settings) =>
        string.IsNullOrEmpty(settings.ConnectionKey) ? MicrosoftAuthenticationConstants.GraphConnectionKey : settings.ConnectionKey;
}
