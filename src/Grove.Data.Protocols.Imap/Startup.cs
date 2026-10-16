using Grove.Communications.Email;
using Grove.Environment.Options;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Grove.Data.Protocols.Imap;

public sealed class Startup
{
    public void ConfigureServices(IServiceCollection services)
    {
        services.AddConnectorProtocol<ImapConnectorProtocol>();

        services.AddEmailReceiveProviderOptionsConfiguration<ImapReceiveProviderConfiguration>()
            .AddSignalOptionsChangeTokenSource<ImapSettings>()
            .AddTransient<IConfigureOptions<ImapSettings>, ImapSettingsConfiguration>();

        services.AddSettingsModel<ImapSettings>(EmailSettings.GroupId, "IMAP", model => model
            .At(200)
            .ValidatedBy((_, settings, errors, _) =>
            {
                if (settings.IsEnabled && string.IsNullOrWhiteSpace(settings.ConnectionKey))
                {
                    errors.Add(nameof(ImapSettings.ConnectionKey), "Pick the IMAP connection to read through.");
                }

                return Task.CompletedTask;
            })
            .OnSaved(async (services, settings, _) =>
            {
                services.GetRequiredService<IOptionsUpdateNotifier>().RequestUpdate<ImapSettings>();
                await EmailProviderDefaults.UpdateDefaultReceiveProviderAsync(services, ImapEmailReceiveProvider.TechnicalName, settings.IsEnabled);
            }));
    }
}
