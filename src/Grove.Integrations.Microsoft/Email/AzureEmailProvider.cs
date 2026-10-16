using System.Net.Mail;
using Azure;
using Azure.Communication.Email;
using Azure.Core.Pipeline;
using Grove.Access;
using Grove.Communications.Delivery;
using Grove.Communications.Email;
using Grove.Data.Connectors;
using Grove.Infrastructure;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MailMessage = Grove.Communications.Email.MailMessage;

namespace Grove.Integrations.Microsoft.Email;

/// <summary>
/// The Azure Communication Services e-mail provider: builds the SDK's message and runs the
/// SDK's send inside the connector engine (<see cref="IConnectorEngine.RunSdkAsync{T}"/>) on the
/// picked connection, whose tenant credential is the resource's connection string. The SDK's
/// own retries are off: the engine retries only what it proves was not sent.
/// </summary>
public sealed class AzureEmailProvider : IEmailProvider, IConnectedProvider
{
    public const string TechnicalName = "Azure";

    // Common supported file extensions and their corresponding MIME types for email attachments
    // using Azure Communication Services Email.
    // For more info <see href="https://learn.microsoft.com/en-us/azure/communication-services/concepts/email/email-attachment-allowed-mime-types" />
    internal static readonly Dictionary<string, string> AllowedMimeTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        { ".3gp", "video/3gpp" },
        { ".3g2", "video/3gpp2" },
        { ".7z", "application/x-7z-compressed" },
        { ".aac", "audio/aac" },
        { ".avi", "video/x-msvideo" },
        { ".bmp", "image/bmp" },
        { ".csv", "text/csv" },
        { ".doc", "application/msword" },
        { ".docm", "application/vnd.ms-word.document.macroEnabled.12" },
        { ".docx", "application/vnd.openxmlformats-officedocument.wordprocessingml.document" },
        { ".eot", "application/vnd.ms-fontobject" },
        { ".epub", "application/epub+zip" },
        { ".gif", "image/gif" },
        { ".gz", "application/gzip" },
        { ".ico", "image/vnd.microsoft.icon" },
        { ".ics", "text/calendar" },
        { ".jpg", "image/jpeg" },
        { ".jpeg", "image/jpeg" },
        { ".json", "application/json" },
        { ".mid", ".midi audio/midi" },
        { ".midi", ".midi audio/midi" },
        { ".mp3", "audio/mpeg" },
        { ".mp4", "video/mp4" },
        { ".mpeg", "video/mpeg" },
        { ".oga", "audio/ogg" },
        { ".ogv", "video/ogg" },
        { ".ogx", "application/ogg" },
        { ".one", "application/onenote" },
        { ".opus", "audio/opus" },
        { ".otf", "font/otf" },
        { ".pdf", "application/pdf" },
        { ".png", "image/png" },
        { ".ppsm", "application/vnd.ms-powerpoint.slideshow.macroEnabled.12" },
        { ".ppsx", "application/vnd.openxmlformats-officedocument.presentationml.slideshow" },
        { ".ppt", "application/vnd.ms-powerpoint" },
        { ".pptm", "application/vnd.ms-powerpoint.presentation.macroEnabled.12" },
        { ".pptx", "application/vnd.openxmlformats-officedocument.presentationml.presentation" },
        { ".pub", "application/vnd.ms-publisher" },
        { ".rar", "application/x-rar-compressed" },
        { ".rpmsg", "application/vnd.ms-outlook" },
        { ".rtf", "application/rtf" },
        { ".svg", "image/svg+xml" },
        { ".tar", "application/x-tar" },
        { ".tif", "image/tiff" },
        { ".tiff", "image/tiff" },
        { ".ttf", "font/ttf" },
        { ".txt", "text/plain" },
        { ".vsd", "application/vnd.visio" },
        { ".wav", "audio/wav" },
        { ".weba", "audio/webm" },
        { ".webm", "video/webm" },
        { ".webp", "image/webp" },
        { ".wma", "audio/x-ms-wma" },
        { ".wmv", "video/x-ms-wmv" },
        { ".woff", "font/woff" },
        { ".woff2", "font/woff2" },
        { ".xls", "application/vnd.ms-excel" },
        { ".xlsb", "application/vnd.ms-excel.sheet.binary.macroEnabled.12" },
        { ".xlsm", "application/vnd.ms-excel.sheet.macroEnabled.12" },
        { ".xlsx", "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet" },
        { ".xml", "application/xml" },
        { ".zip", "application/zip" },
    };

    private readonly IOptionsMonitor<AzureEmailOptions> _options;
    private readonly IConnectorEngine _engine;
    private readonly ICallerContextFactory _callers;
    private readonly ILogger _logger;

    private readonly IStringLocalizer S;

    public AzureEmailProvider(
        IOptionsMonitor<AzureEmailOptions> options,
        IConnectorEngine engine,
        ICallerContextFactory callers,
        ILogger<AzureEmailProvider> logger,
        IStringLocalizer<AzureEmailProvider> stringLocalizer)
    {
        _options = options;
        _engine = engine;
        _callers = callers;
        _logger = logger;
        S = stringLocalizer;
    }

    public LocalizedString DisplayName => S["Azure Communication Services"];

    public string ConnectionKey => ConnectionOf(_options.CurrentValue);

    private static string ConnectionOf(AzureEmailOptions options) =>
        string.IsNullOrEmpty(options.ConnectionKey) ? AzureCommunicationServices.EmailConnectionKey : options.ConnectionKey;

    public async Task<Result> SendAsync(MailMessage message, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(message);

        var options = _options.CurrentValue;
        if (!options.IsEnabled)
        {
            return Result.Failed(S["The Azure Email Provider is disabled."]);
        }

        var senderAddress = string.IsNullOrWhiteSpace(message.From) ? options.DefaultSender : message.From;
        if (!string.IsNullOrWhiteSpace(senderAddress))
        {
            if (MailAddress.TryCreate(senderAddress, out var senderMailAddress))
            {
                // Other providers accept a sender with a display name; Azure takes the address.
                message.From = senderMailAddress.Address;
            }
            else
            {
                return Result.Failed(new ResultError
                {
                    Key = nameof(message.From),
                    Message = S["Invalid email address for the sender: '{0}'.", senderAddress],
                });
            }
        }

        var errors = new Dictionary<string, IList<LocalizedString>>();
        var emailMessage = FromMailMessage(message, errors);
        if (errors.Count > 0)
        {
            return Result.Failed(errors.SelectMany(entry => entry.Value.Select(error => new ResultError { Key = entry.Key, Message = error })));
        }

        var caller = await _callers.CreateSystemAsync(cancellationToken: cancellationToken);
        var result = await _engine.RunSdkAsync(
            ConnectionOf(options),
            caller,
            async (context, token) =>
            {
                var connectionString = context.Credential?[CredentialKeys.ConnectionString]
                    ?? throw new ConnectorRefusedException("The Azure Communication Services connection has no connection string.");
                var clientOptions = new EmailClientOptions { Transport = new HttpClientTransport(context.HttpClient) };
                clientOptions.Retry.MaxRetries = 0;

                var operation = await new EmailClient(connectionString, clientOptions).SendAsync(WaitUntil.Started, emailMessage, token);
                return operation.Id;
            },
            new SdkCallOptions { IsSafeToRetry = false, Classify = AzureCommunicationServices.Classify },
            cancellationToken);

        if (result.Succeeded)
        {
            return DeliveryResult.From(result, ConnectionOf(options), succeeded: true);
        }

        // Never the SDK's message: it may carry the connection string.
        _logger.LogError("Azure Communication Services did not send an email: {Outcome} {StatusCode} {Failure}", result.Outcome, result.StatusCode, result.Failure);

        return DeliveryResult.From(result, ConnectionOf(options), succeeded: false, new ResultError
        {
            Message = result.Outcome == ConnectorOutcome.NotAuthorized
                ? S["The Azure Communication Services connection has no usable credential."]
                : S["An error occurred while sending an email."],
        });
    }

    private EmailMessage FromMailMessage(MailMessage message, Dictionary<string, IList<LocalizedString>> errors)
    {
        var recipients = message.GetRecipients();

        var toRecipients = new List<EmailAddress>();
        foreach (var toRecipient in recipients.To)
        {
            if (MailAddress.TryCreate(toRecipient, out var toMailAddress))
            {
                toRecipients.Add(ConvertMailAddressToAzureEmailAddress(toMailAddress));
            }
            else
            {
                Add(errors, nameof(recipients.To), S["Invalid email address for the 'To' recipient: '{0}'.", toRecipient]);
            }
        }

        var ccRecipients = new List<EmailAddress>();
        foreach (var ccRecipient in recipients.Cc)
        {
            if (MailAddress.TryCreate(ccRecipient, out var ccMailAddress))
            {
                ccRecipients.Add(ConvertMailAddressToAzureEmailAddress(ccMailAddress));
            }
            else
            {
                Add(errors, nameof(recipients.Cc), S["Invalid email address for the 'CC' recipient: '{0}'.", ccRecipient]);
            }
        }

        var bccRecipients = new List<EmailAddress>();
        foreach (var bccRecipient in recipients.Bcc)
        {
            if (MailAddress.TryCreate(bccRecipient, out var bccMailAddress))
            {
                bccRecipients.Add(ConvertMailAddressToAzureEmailAddress(bccMailAddress));
            }
            else
            {
                Add(errors, nameof(recipients.Bcc), S["Invalid email address for the 'BCC' recipient: '{0}'.", bccRecipient]);
            }
        }

        var content = new EmailContent(message.Subject ?? string.Empty)
        {
            PlainText = message.TextBody,
            Html = message.HtmlBody,
        };

        var emailMessage = new EmailMessage(
            message.From,
            new EmailRecipients(toRecipients, ccRecipients, bccRecipients),
            content);

        foreach (var replyTo in message.GetReplyTo())
        {
            if (MailAddress.TryCreate(replyTo, out var replyToMailAddress))
            {
                emailMessage.ReplyTo.Add(ConvertMailAddressToAzureEmailAddress(replyToMailAddress));
            }
            else
            {
                Add(errors, nameof(emailMessage.ReplyTo), S["Invalid email address to reply to: '{0}'.", replyTo]);
            }
        }

        foreach (var attachment in message.Attachments)
        {
            if (attachment.Stream == null)
            {
                continue;
            }
            var extension = Path.GetExtension(attachment.Filename);

            if (AllowedMimeTypes.TryGetValue(extension, out var contentType))
            {
                var data = new byte[attachment.Stream.Length];

                attachment.Stream.ReadExactly(data);

                emailMessage.Attachments.Add(new EmailAttachment(attachment.Filename, contentType, new BinaryData(data)));
            }
            else
            {
                Add(errors, nameof(message.Attachments), S["Unable to attach the file named '{0}' since its type is not supported.", attachment.Filename]);

                _logger.LogWarning("The MIME type for the attachment '{Attachment}' is not supported.", attachment.Filename);
            }
        }

        return emailMessage;
    }

    private static EmailAddress ConvertMailAddressToAzureEmailAddress(MailAddress mailAddress) =>
        new EmailAddress(mailAddress.Address, mailAddress.DisplayName);

    private static void Add(Dictionary<string, IList<LocalizedString>> errors, string key, LocalizedString error)
    {
        if (!errors.TryGetValue(key, out var list))
        {
            errors[key] = list = [];
        }

        list.Add(error);
    }
}
