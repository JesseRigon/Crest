using Grove.Communications.Email;
using Grove.Environment.Options;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Grove.Data.Protocols.Pop;

public sealed class Startup
{
    public void ConfigureServices(IServiceCollection services)
    {
        services.AddConnectorProtocol<PopConnectorProtocol>();

        services.AddEmailReceiveProviderOptionsConfiguration<PopReceiveProviderConfiguration>()
            .AddSignalOptionsChangeTokenSource<PopSettings>()
            .AddTransient<IConfigureOptions<PopSettings>, PopSettingsConfiguration>();

        services.AddSettingsModel<PopSettings>(EmailSettings.GroupId, "POP3", model => model
            .At(210)
            .ValidatedBy((_, settings, errors, _) =>
            {
                if (settings.IsEnabled && string.IsNullOrWhiteSpace(settings.ConnectionKey))
                {
                    errors.Add(nameof(PopSettings.ConnectionKey), "Pick the POP3 connection to read through.");
                }

                return Task.CompletedTask;
            })
            .OnSaved(async (services, settings, _) =>
            {
                services.GetRequiredService<IOptionsUpdateNotifier>().RequestUpdate<PopSettings>();
                await EmailProviderDefaults.UpdateDefaultReceiveProviderAsync(services, PopEmailReceiveProvider.TechnicalName, settings.IsEnabled);
            }));
    }
}
