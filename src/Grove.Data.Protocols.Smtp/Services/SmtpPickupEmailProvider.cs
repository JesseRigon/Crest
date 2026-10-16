using Grove.Communications.Email;
using Grove.Infrastructure;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Options;
using MimeKit;

namespace Grove.Data.Protocols.Smtp.Services;

/// <summary>
/// The host's pickup-directory delivery (<see cref="DefaultSmtpOptions"/>, from the shell
/// configuration): writes each message as an <c>.eml</c> file instead of sending it. For
/// development and test hosts; it makes no external call, so it uses no connection.
/// </summary>
public sealed class SmtpPickupEmailProvider : IEmailProvider
{
    public const string TechnicalName = "SmtpPickup";

    private const string EmailExtension = ".eml";

    private readonly IOptionsMonitor<DefaultSmtpOptions> _options;
    private readonly IEmailAddressValidator _emailAddressValidator;

    private readonly IStringLocalizer S;

    public SmtpPickupEmailProvider(
        IOptionsMonitor<DefaultSmtpOptions> options,
        IEmailAddressValidator emailAddressValidator,
        IStringLocalizer<SmtpPickupEmailProvider> stringLocalizer)
    {
        _options = options;
        _emailAddressValidator = emailAddressValidator;
        S = stringLocalizer;
    }

    public LocalizedString DisplayName => S["SMTP pickup directory (the host's configuration)"];

    public async Task<Result> SendAsync(MailMessage message, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(message);

        var options = _options.CurrentValue;
        if (!options.IsEnabled || string.IsNullOrEmpty(options.PickupDirectoryLocation))
        {
            return Result.Failed(S["The SMTP pickup directory is not configured."]);
        }

        var mime = SmtpMessages.Create(message, options.DefaultSender, _emailAddressValidator, S);
        if (!mime.Succeeded)
        {
            return mime;
        }

        try
        {
            await WriteAsync(mime.Value, options.PickupDirectoryLocation, cancellationToken);
            return Result.Success();
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return Result.Failed(S["An error occurred while sending an email: '{0}'", exception.Message]);
        }
    }

    private static Task WriteAsync(MimeMessage message, string pickupDirectory, CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(pickupDirectory);

        return message.WriteToAsync(Path.Combine(pickupDirectory, Guid.NewGuid().ToString() + EmailExtension), cancellationToken);
    }
}
