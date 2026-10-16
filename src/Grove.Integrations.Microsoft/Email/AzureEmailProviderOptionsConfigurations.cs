using Grove.Communications.Email.Services;
using Microsoft.Extensions.Options;

namespace Grove.Integrations.Microsoft.Email;

/// <summary>Registers the Azure provider in the e-mail channel's registry.</summary>
public sealed class AzureEmailProviderOptionsConfigurations(IOptionsMonitor<AzureEmailOptions> azureOptions) : IConfigureOptions<EmailProviderOptions>
{
    public void Configure(EmailProviderOptions options) =>
        options.TryAddProvider(AzureEmailProvider.TechnicalName, new EmailProviderTypeOptions(typeof(AzureEmailProvider))
        {
            IsEnabled = azureOptions.CurrentValue.IsEnabled,
        });
}
