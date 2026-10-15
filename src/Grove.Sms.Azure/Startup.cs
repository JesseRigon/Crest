using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Grove.DisplayManagement.Handlers;
using Grove.Environment.Options;
using Grove.Environment.Shell.Configuration;
using Grove.Modules;
using Grove.Sms.Azure.Models;
using Grove.Sms.Azure.Services;

namespace Grove.Sms.Azure;

public sealed class Startup : StartupBase
{
    private readonly IShellConfiguration _shellConfiguration;

    public Startup(IShellConfiguration shellConfiguration)
    {
        _shellConfiguration = shellConfiguration;
    }

    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddAzureSmsProvider();
        services.AddSettingsModel<AzureSmsSettings>(SmsSettings.GroupId, "Azure Communication Services", model => model
            .ValidatedBy((services, settings, errors, _) =>
            {
                if (settings.IsEnabled)
                {
                    SmsProviderSettings.ValidatePhoneNumber(services, settings.PhoneNumber, nameof(AzureSmsSettings.PhoneNumber), errors);

                    if (string.IsNullOrWhiteSpace(settings.ConnectionString))
                    {
                        errors.Add(nameof(AzureSmsSettings.ConnectionString), "Connection string is required.");
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

        services.Configure<DefaultAzureSmsOptions>(options =>
        {
            _shellConfiguration.GetSection("Grove_Sms_AzureCommunicationServices").Bind(options);

            options.IsEnabled = options.ConfigurationExists();
        });
    }
}
