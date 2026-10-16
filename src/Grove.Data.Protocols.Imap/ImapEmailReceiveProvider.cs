using Grove.Access;
using Grove.Communications.Email.Services;
using Grove.Data.Connectors;
using Grove.Data.Protocols.Smtp.Receiving;
using Grove.Infrastructure.Settings;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Options;

namespace Grove.Data.Protocols.Imap;

/// <summary>Reads the tenant's IMAP mailbox through the picked connection.</summary>
public sealed class ImapEmailReceiveProvider(
    IOptionsMonitor<ImapSettings> settings,
    IConnectorEngine engine,
    ICallerContextFactory callers,
    IStringLocalizer<ImapEmailReceiveProvider> stringLocalizer)
    : MailboxReceiveProvider(engine, callers, stringLocalizer)
{
    public const string TechnicalName = "IMAP";

    public override LocalizedString DisplayName => S["Internet Message Access Protocol (IMAP)"];

    protected override MailboxTarget Target
    {
        get
        {
            var current = settings.CurrentValue;
            return new MailboxTarget(current.IsEnabled, current.ConnectionKey, string.IsNullOrWhiteSpace(current.Folder) ? null : current.Folder);
        }
    }
}

/// <summary>The IMAP provider's options: its settings, read from the site.</summary>
public sealed class ImapSettingsConfiguration(ISiteService siteService) : IConfigureOptions<ImapSettings>
{
    public void Configure(ImapSettings options)
    {
        var settings = siteService.GetSettings<ImapSettings>();
        options.IsEnabled = settings.IsEnabled && !string.IsNullOrEmpty(settings.ConnectionKey);
        options.ConnectionKey = settings.ConnectionKey;
        options.Folder = settings.Folder;
    }
}

/// <summary>Registers the IMAP receive provider in the e-mail channel's receive registry.</summary>
public sealed class ImapReceiveProviderConfiguration(IOptionsMonitor<ImapSettings> settings) : IConfigureOptions<EmailReceiveProviderOptions>
{
    public void Configure(EmailReceiveProviderOptions options) =>
        options.TryAddProvider(ImapEmailReceiveProvider.TechnicalName, new EmailReceiveProviderTypeOptions(typeof(ImapEmailReceiveProvider))
        {
            IsEnabled = settings.CurrentValue.IsEnabled,
        });
}
