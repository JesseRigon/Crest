using Grove.Access;
using Grove.Communications.Email.Services;
using Grove.Data.Connectors;
using Grove.Data.Protocols.Smtp.Receiving;
using Grove.Infrastructure.Settings;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Options;

namespace Grove.Data.Protocols.Pop;

/// <summary>Reads the tenant's POP3 maildrop through the picked connection.</summary>
public sealed class PopEmailReceiveProvider(
    IOptionsMonitor<PopSettings> settings,
    IConnectorEngine engine,
    ICallerContextFactory callers,
    IStringLocalizer<PopEmailReceiveProvider> stringLocalizer)
    : MailboxReceiveProvider(engine, callers, stringLocalizer)
{
    public const string TechnicalName = "POP3";

    public override LocalizedString DisplayName => S["Post Office Protocol (POP3)"];

    protected override MailboxTarget Target
    {
        get
        {
            var current = settings.CurrentValue;
            return new MailboxTarget(current.IsEnabled, current.ConnectionKey, null);
        }
    }
}

/// <summary>The POP3 provider's options: its settings, read from the site.</summary>
public sealed class PopSettingsConfiguration(ISiteService siteService) : IConfigureOptions<PopSettings>
{
    public void Configure(PopSettings options)
    {
        var settings = siteService.GetSettings<PopSettings>();
        options.IsEnabled = settings.IsEnabled && !string.IsNullOrEmpty(settings.ConnectionKey);
        options.ConnectionKey = settings.ConnectionKey;
    }
}

/// <summary>Registers the POP3 receive provider in the e-mail channel's receive registry.</summary>
public sealed class PopReceiveProviderConfiguration(IOptionsMonitor<PopSettings> settings) : IConfigureOptions<EmailReceiveProviderOptions>
{
    public void Configure(EmailReceiveProviderOptions options) =>
        options.TryAddProvider(PopEmailReceiveProvider.TechnicalName, new EmailReceiveProviderTypeOptions(typeof(PopEmailReceiveProvider))
        {
            IsEnabled = settings.CurrentValue.IsEnabled,
        });
}
