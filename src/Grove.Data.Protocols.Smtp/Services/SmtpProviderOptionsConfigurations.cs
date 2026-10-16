using Grove.Communications.Email.Services;
using Microsoft.Extensions.Options;

namespace Grove.Data.Protocols.Smtp.Services;

/// <summary>Registers the SMTP provider, and the host's pickup delivery when it is configured.</summary>
public sealed class SmtpProviderOptionsConfigurations(
    IOptionsMonitor<SmtpOptions> smtpOptions,
    IOptionsMonitor<DefaultSmtpOptions> defaultSmtpOptions) : IConfigureOptions<EmailProviderOptions>
{
    public void Configure(EmailProviderOptions options)
    {
        options.TryAddProvider(SmtpEmailProvider.TechnicalName, new EmailProviderTypeOptions(typeof(SmtpEmailProvider))
        {
            IsEnabled = smtpOptions.CurrentValue.IsEnabled,
        });

        if (defaultSmtpOptions.CurrentValue.IsEnabled)
        {
            options.TryAddProvider(SmtpPickupEmailProvider.TechnicalName, new EmailProviderTypeOptions(typeof(SmtpPickupEmailProvider))
            {
                IsEnabled = true,
            });
        }
    }
}
