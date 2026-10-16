using Grove.Communications.Sms;
using Grove.Environment.Options;
using Grove.Integrations.Twilio.Models;
using Grove.Integrations.Twilio.Services;
using Grove.Modules;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Grove.Integrations.Twilio;

public sealed class Startup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddConnection(TwilioConnection.Key, TwilioConnection.Declare);

        services.AddSignalOptionsChangeTokenSource<TwilioOptions>()
            .AddTransient<IConfigureOptions<SmsProviderOptions>, TwilioProviderOptionsConfigurations>()
            .AddTransient<IConfigureOptions<TwilioOptions>, TwilioOptionsConfiguration>();

        services.AddSettingsModel<TwilioSettings>(SmsSettings.GroupId, "Twilio", model => model
            .ValidatedBy((services, settings, errors, _) =>
            {
                if (settings.IsEnabled)
                {
                    SmsProviderSettings.ValidatePhoneNumber(services, settings.PhoneNumber, nameof(TwilioSettings.PhoneNumber), errors);

                    if (string.IsNullOrWhiteSpace(settings.AccountSID))
                    {
                        errors.Add(nameof(TwilioSettings.AccountSID), "Account SID requires a value.");
                    }

                    if (string.IsNullOrWhiteSpace(settings.ConnectionKey))
                    {
                        errors.Add(nameof(TwilioSettings.ConnectionKey), "Pick the Twilio connection to send through.");
                    }
                }

                return Task.CompletedTask;
            })
            .OnSaved(async (services, settings, _) =>
            {
                await SmsProviderSettings.UpdateDefaultProviderAsync(services, TwilioSmsProvider.TechnicalName, settings.IsEnabled);
                services.GetRequiredService<IOptionsUpdateNotifier>()
                    .RequestUpdate<TwilioOptions>()
                    .RequestUpdate<SmsProviderOptions>();
            }));
    }
}
