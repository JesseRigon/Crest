using Grove.Communications.Email;
using Grove.Communications.Email.Services;
using Grove.Environment.Options;
using Grove.Modules;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Grove.Integrations.Microsoft.Email;

[Feature(AzureCommunicationServices.Features.Email)]
public sealed class AzureEmailStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddConnection(AzureCommunicationServices.EmailConnectionKey, AzureCommunicationServices.DeclareEmail);

        services.AddSignalOptionsChangeTokenSource<AzureEmailOptions>()
            .AddTransient<IConfigureOptions<AzureEmailOptions>, AzureEmailOptionsConfiguration>()
            .AddTransient<IConfigureOptions<EmailProviderOptions>, AzureEmailProviderOptionsConfigurations>();

        services.AddSettingsModel<AzureEmailSettings>(EmailSettings.GroupId, "Azure Communication Services", model => model
            .At(110)
            .ValidatedBy((services, settings, errors, _) =>
            {
                if (settings.IsEnabled)
                {
                    if (string.IsNullOrWhiteSpace(settings.DefaultSender) || !services.GetRequiredService<IEmailAddressValidator>().Validate(settings.DefaultSender))
                    {
                        errors.Add(nameof(AzureEmailSettings.DefaultSender), "A valid default sender is required.");
                    }

                    if (string.IsNullOrWhiteSpace(settings.ConnectionKey))
                    {
                        errors.Add(nameof(AzureEmailSettings.ConnectionKey), "Pick the connection to send through.");
                    }
                }

                return Task.CompletedTask;
            })
            .OnSaved(async (services, settings, _) =>
            {
                services.GetRequiredService<IOptionsUpdateNotifier>().RequestUpdate<AzureEmailOptions>();
                await EmailProviderDefaults.UpdateDefaultProviderAsync(services, AzureEmailProvider.TechnicalName, settings.IsEnabled);
            }));
    }
}
