using System.Text.Json.Nodes;
using Grove.Communications.Email;
using Grove.Data.Connectors;
using MimeKit;

namespace Grove.Data.Protocols.Smtp.Receiving;

/// <summary>
/// The wire shape the mailbox protocols (<c>imap</c>, <c>pop</c>) share with their receive
/// providers. A read is a raw request with method <see cref="FetchMethod"/> and the arguments
/// <c>folder</c>, <c>cursor</c> and <c>max</c>; it answers <c>{ messages: [{ id, mime }], cursor, hasMore }</c>
/// with each message's raw MIME in base64. A removal is method <see cref="DeleteMethod"/> with <c>ids</c>.
/// </summary>
public static class Mailbox
{
    /// <summary>Reads messages; changes nothing on the server.</summary>
    public const string FetchMethod = "FETCH";

    /// <summary>Removes messages by id; not safe to repeat blindly (ids may be reused).</summary>
    public const string DeleteMethod = "DELETE";

    public const string Folder = "folder";
    public const string Cursor = "cursor";
    public const string Max = "max";
    public const string Ids = "ids";
    public const string Messages = "messages";
    public const string Id = "id";
    public const string Mime = "mime";
    public const string HasMore = "hasMore";

    public static readonly ConnectionOperation FetchOperation = new()
    {
        Id = "fetch",
        DisplayName = "Read messages",
        Description = "Reads the messages after a cursor, oldest first, as raw MIME.",
        Kind = "mail-fetch",
        Method = FetchMethod,
        IsSafeToRetry = true,
    };

    public static readonly ConnectionOperation DeleteOperation = new()
    {
        Id = "delete",
        DisplayName = "Delete messages",
        Description = "Removes messages from the mailbox by id.",
        Kind = "mail-delete",
        Method = DeleteMethod,
        IsSafeToRetry = false,
    };

    /// <summary>The arguments of a read.</summary>
    public static JsonObject FetchArguments(string? folder, string? cursor, int max) => new()
    {
        [Folder] = folder,
        [Cursor] = cursor,
        [Max] = max,
    };

    /// <summary>The arguments of a removal.</summary>
    public static JsonObject DeleteArguments(string? folder, IEnumerable<string> ids) => new()
    {
        [Folder] = folder,
        [Ids] = new JsonArray([.. ids.Select(id => (JsonNode?)JsonValue.Create(id))]),
    };

    /// <summary>A read's answer.</summary>
    public static JsonObject FetchResponse(IEnumerable<(string Id, byte[] Mime)> messages, string? cursor, bool hasMore) => new()
    {
        [Messages] = new JsonArray([.. messages.Select(message => (JsonNode?)new JsonObject
        {
            [Id] = message.Id,
            [Mime] = Convert.ToBase64String(message.Mime),
        })]),
        [Cursor] = cursor,
        [HasMore] = hasMore,
    };

    /// <summary>Reads a read's answer into the channel's received messages.</summary>
    public static EmailReceiveBatch ReadBatch(JsonNode? value, string? previousCursor)
    {
        var messages = new List<ReceivedMailMessage>();
        if (value?[Messages] is JsonArray array)
        {
            foreach (var item in array.OfType<JsonObject>())
            {
                var id = item[Id]?.GetValue<string>();
                var mime = item[Mime]?.GetValue<string>();
                if (id is null || mime is null)
                {
                    continue;
                }

                using var stream = new MemoryStream(Convert.FromBase64String(mime));
                messages.Add(ToReceived(id, MimeMessage.Load(stream)));
            }
        }

        return new EmailReceiveBatch(messages, value?[Cursor]?.GetValue<string>() ?? previousCursor)
        {
            HasMore = value?[HasMore]?.GetValue<bool>() == true,
        };
    }

    /// <summary>A MIME message as the channel's received message.</summary>
    public static ReceivedMailMessage ToReceived(string id, MimeMessage message)
    {
        ArgumentNullException.ThrowIfNull(message);

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

    /// <summary>The raw bytes of a MIME message.</summary>
    public static async Task<byte[]> ToBytesAsync(MimeMessage message, CancellationToken cancellationToken)
    {
        using var stream = new MemoryStream();
        await message.WriteToAsync(stream, cancellationToken);
        return stream.ToArray();
    }
}
