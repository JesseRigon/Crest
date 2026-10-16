using Grove.Communications.Sms;
using Grove.Integrations.Twilio.Models;
using Microsoft.Extensions.Options;

namespace Grove.Integrations.Twilio.Services;

/// <summary>Registers the Twilio provider in the SMS channel's registry.</summary>
public sealed class TwilioProviderOptionsConfigurations(IOptionsMonitor<TwilioOptions> twilioOptions) : IConfigureOptions<SmsProviderOptions>
{
    public void Configure(SmsProviderOptions options) =>
        options.TryAddProvider(TwilioSmsProvider.TechnicalName, new SmsProviderTypeOptions(typeof(TwilioSmsProvider))
        {
            IsEnabled = twilioOptions.CurrentValue.IsEnabled,
        });
}
