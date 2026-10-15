using Microsoft.Extensions.Options;
using Grove.Settings;
using Grove.Sms.Models;

namespace Grove.Sms.Services;

public sealed class TwilioProviderOptionsConfigurations : IConfigureOptions<SmsProviderOptions>
{
    private readonly IOptionsMonitor<TwilioOptions> _twilioOptions;

    public TwilioProviderOptionsConfigurations(IOptionsMonitor<TwilioOptions> twilioOptions)
    {
        _twilioOptions = twilioOptions;
    }

    public void Configure(SmsProviderOptions options)
    {
        var typeOptions = new SmsProviderTypeOptions(typeof(TwilioSmsProvider));
        typeOptions.IsEnabled = _twilioOptions.CurrentValue.IsEnabled;

        options.TryAddProvider(TwilioSmsProvider.TechnicalName, typeOptions);
    }
}
