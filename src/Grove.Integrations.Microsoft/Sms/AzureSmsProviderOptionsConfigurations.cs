using Grove.Communications.Sms;
using Microsoft.Extensions.Options;

namespace Grove.Integrations.Microsoft.Sms;

/// <summary>Registers the Azure provider in the SMS channel's registry.</summary>
public sealed class AzureSmsProviderOptionsConfigurations(IOptionsMonitor<AzureSmsOptions> azureOptions) : IConfigureOptions<SmsProviderOptions>
{
    public void Configure(SmsProviderOptions options) =>
        options.TryAddProvider(AzureSmsProvider.TechnicalName, new SmsProviderTypeOptions(typeof(AzureSmsProvider))
        {
            IsEnabled = azureOptions.CurrentValue.IsEnabled,
        });
}
