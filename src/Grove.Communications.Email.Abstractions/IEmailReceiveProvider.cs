using Grove.Infrastructure;
using Microsoft.Extensions.Localization;

namespace Grove.Communications.Email;

/// <summary>
/// A receive provider of the e-mail channel: reads a mailbox (IMAP, POP3, a vendor mailbox)
/// through its connection. Registered by technical name in <see cref="Services.EmailReceiveProviderOptions"/>.
/// </summary>
public interface IEmailReceiveProvider
{
    LocalizedString DisplayName { get; }

    /// <summary>
    /// Reads the messages that arrived after <see cref="EmailReceiveRequest.Cursor"/>, at most
    /// <see cref="EmailReceiveRequest.MaxMessages"/>. The batch's cursor is passed back on the next call.
    /// </summary>
    Task<Result<EmailReceiveBatch>> ReceiveAsync(EmailReceiveRequest request, CancellationToken cancellationToken = default);

    /// <summary>Removes received messages from the mailbox, by <see cref="ReceivedMailMessage.Id"/>.</summary>
    Task<Result> DeleteAsync(IReadOnlyList<string> messageIds, CancellationToken cancellationToken = default);
}

/// <summary>What to read from a mailbox.</summary>
public sealed record EmailReceiveRequest
{
    /// <summary>The position the previous batch ended at; null reads from the oldest message.</summary>
    public string Cursor { get; init; }

    public int MaxMessages { get; init; } = 50;

    /// <summary>The folder to read (IMAP); null for the provider's configured one.</summary>
    public string Folder { get; init; }
}

/// <summary>A batch of received messages and the cursor to continue from.</summary>
public sealed record EmailReceiveBatch(IReadOnlyList<ReceivedMailMessage> Messages, string Cursor)
{
    /// <summary>True when the mailbox has more messages after this batch.</summary>
    public bool HasMore { get; init; }
}

/// <summary>A message read from a mailbox.</summary>
public sealed class ReceivedMailMessage
{
    /// <summary>The provider's id of the message in the mailbox (an IMAP UID, a POP3 UIDL).</summary>
    public string Id { get; init; }

    /// <summary>The Message-ID header.</summary>
    public string MessageId { get; init; }

    public string From { get; init; }

    public string To { get; init; }

    public string Cc { get; init; }

    public string ReplyTo { get; init; }

    public string Subject { get; init; }

    public string TextBody { get; init; }

    public string HtmlBody { get; init; }

    public DateTimeOffset? Date { get; init; }

    public IReadOnlyList<ReceivedMailAttachment> Attachments { get; init; } = [];
}

/// <summary>An attachment of a received message.</summary>
public sealed record ReceivedMailAttachment(string Filename, string ContentType, byte[] Content);
