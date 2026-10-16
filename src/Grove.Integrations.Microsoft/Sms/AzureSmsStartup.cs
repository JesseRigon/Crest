using Grove.Communications.Sms;
using Grove.Environment.Options;
using Grove.Modules;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Grove.Integrations.Microsoft.Sms;

[Feature(AzureCommunicationServices.Features.Sms)]
public sealed class AzureSmsStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddConnection(AzureCommunicationServices.SmsConnectionKey, AzureCommunicationServices.DeclareSms);

        services.AddSignalOptionsChangeTokenSource<AzureSmsOptions>()
            .AddTransient<IConfigureOptions<AzureSmsOptions>, AzureSmsOptionsConfiguration>()
            .AddTransient<IConfigureOptions<SmsProviderOptions>, AzureSmsProviderOptionsConfigurations>();

        services.AddSettingsModel<AzureSmsSettings>(SmsSettings.GroupId, "Azure Communication Services", model => model
            .ValidatedBy((services, settings, errors, _) =>
            {
                if (settings.IsEnabled)
                {
                    SmsProviderSettings.ValidatePhoneNumber(services, settings.PhoneNumber, nameof(AzureSmsSettings.PhoneNumber), errors);

                    if (string.IsNullOrWhiteSpace(settings.ConnectionKey))
                    {
                        errors.Add(nameof(AzureSmsSettings.ConnectionKey), "Pick the connection to send through.");
                    }
                }

                return Task.CompletedTask;
            })
            .OnSaved(async (services, settings, _) =>
            {
                await SmsProviderSettings.UpdateDefaultProviderAsync(services, AzureSmsProvider.TechnicalName, settings.IsEnabled);
                services.GetRequiredService<IOptionsUpdateNotifier>()
                    .RequestUpdate<AzureSmsOptions>()
                    .RequestUpdate<SmsProviderOptions>();
            }));
    }
}
