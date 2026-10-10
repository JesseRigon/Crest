using Microsoft.Extensions.Options;
using Crest.Settings;
using Crest.Sms.Models;

namespace Crest.Sms.Services;

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
