using Grove.Communications.Email;
using Grove.Infrastructure;
using Microsoft.Extensions.Localization;
using MimeKit;

namespace Grove.Data.Protocols.Smtp.Services;

/// <summary>Turns the channel's <see cref="MailMessage"/> into the MIME message SMTP carries.</summary>
public static class SmtpMessages
{
    /// <summary>
    /// Builds the MIME message; the author is the message's <see cref="MailMessage.From"/> or
    /// <paramref name="defaultSender"/>. Fails when the sender is not a valid address.
    /// </summary>
    public static Result<MimeMessage> Create(MailMessage message, string defaultSender, IEmailAddressValidator validator, IStringLocalizer S)
    {
        ArgumentNullException.ThrowIfNull(message);

        var senderAddress = string.IsNullOrWhiteSpace(message.From) ? defaultSender : message.From;
        if (!string.IsNullOrWhiteSpace(senderAddress))
        {
            if (!validator.Validate(senderAddress))
            {
                return Result.Failed<MimeMessage>(new ResultError
                {
                    Key = nameof(message.From),
                    Message = S["Invalid email address for the sender: '{0}'.", senderAddress],
                });
            }

            // The author (From), not the submitter (Sender).
            message.From = senderAddress;
        }

        var mimeMessage = new MimeMessage();
        var submitterAddress = string.IsNullOrWhiteSpace(message.Sender) ? defaultSender : message.Sender;
        if (!string.IsNullOrEmpty(submitterAddress))
        {
            mimeMessage.Sender = MailboxAddress.Parse(submitterAddress);
        }

        mimeMessage.From.AddRange(message.GetSender().Select(MailboxAddress.Parse));

        var recipients = message.GetRecipients();
        mimeMessage.To.AddRange(recipients.To.Select(MailboxAddress.Parse));
        mimeMessage.Cc.AddRange(recipients.Cc.Select(MailboxAddress.Parse));
        mimeMessage.Bcc.AddRange(recipients.Bcc.Select(MailboxAddress.Parse));
        mimeMessage.ReplyTo.AddRange(message.GetReplyTo().Select(MailboxAddress.Parse));
        mimeMessage.Subject = message.Subject ?? string.Empty;

        var body = new BodyBuilder
        {
            TextBody = message.TextBody,
            HtmlBody = message.HtmlBody,
        };

        foreach (var attachment in message.Attachments)
        {
            // A null stream would make MimeKit read the file system path.
            if (attachment.Stream != null)
            {
                body.Attachments.Add(attachment.Filename, attachment.Stream);
            }
        }

        mimeMessage.Body = body.ToMessageBody();

        return Result.Success(mimeMessage);
    }

    /// <summary>The message as 7-bit MIME text: the body of an SMTP send through the engine.</summary>
    public static async Task<string> SerializeAsync(MimeMessage message, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(message);

        message.Prepare(EncodingConstraint.SevenBit);
        using var stream = new MemoryStream();
        await message.WriteToAsync(stream, cancellationToken);

        return System.Text.Encoding.ASCII.GetString(stream.ToArray());
    }
}
