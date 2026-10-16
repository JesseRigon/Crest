using Grove.Communications.Email;
using Grove.Communications.Email.Services;
using Grove.Environment.Options;
using Grove.Infrastructure.Settings;
using Grove.Modules;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Grove.Integrations.Microsoft.GraphMail;

[Feature(MicrosoftAuthenticationConstants.Features.GraphMail)]
public sealed class GraphMailStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddSignalOptionsChangeTokenSource<GraphMailSettings>()
            .AddTransient<IConfigureOptions<GraphMailSettings>, GraphMailSettingsConfiguration>()
            .AddTransient<IConfigureOptions<EmailProviderOptions>, GraphMailProviderConfiguration>();

        services.AddSettingsModel<GraphMailSettings>(EmailSettings.GroupId, "Microsoft Graph", model => model
            .At(120)
            .ValidatedBy((_, settings, errors, _) =>
            {
                if (settings.IsEnabled && string.IsNullOrWhiteSpace(settings.ConnectionKey))
                {
                    errors.Add(nameof(GraphMailSettings.ConnectionKey), "Pick the connection to send through.");
                }

                return Task.CompletedTask;
            })
            .OnSaved(async (services, settings, _) =>
            {
                services.GetRequiredService<IOptionsUpdateNotifier>().RequestUpdate<GraphMailSettings>();
                await EmailProviderDefaults.UpdateDefaultProviderAsync(services, GraphMailProvider.TechnicalName, settings.IsEnabled);
            }));
    }
}

[Feature(MicrosoftAuthenticationConstants.Features.GraphMailbox)]
public sealed class GraphMailboxStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddSignalOptionsChangeTokenSource<GraphMailboxSettings>()
            .AddTransient<IConfigureOptions<GraphMailboxSettings>, GraphMailboxSettingsConfiguration>()
            .AddTransient<IConfigureOptions<EmailReceiveProviderOptions>, GraphMailboxProviderConfiguration>();

        services.AddSettingsModel<GraphMailboxSettings>(EmailSettings.GroupId, "Microsoft Graph mailbox", model => model
            .At(220)
            .ValidatedBy((_, settings, errors, _) =>
            {
                if (settings.IsEnabled && string.IsNullOrWhiteSpace(settings.ConnectionKey))
                {
                    errors.Add(nameof(GraphMailboxSettings.ConnectionKey), "Pick the connection to read through.");
                }

                return Task.CompletedTask;
            })
            .OnSaved(async (services, settings, _) =>
            {
                services.GetRequiredService<IOptionsUpdateNotifier>().RequestUpdate<GraphMailboxSettings>();
                await EmailProviderDefaults.UpdateDefaultReceiveProviderAsync(services, GraphMailboxReceiveProvider.TechnicalName, settings.IsEnabled);
            }));
    }
}

public sealed class GraphMailSettingsConfiguration(ISiteService siteService) : IConfigureOptions<GraphMailSettings>
{
    public void Configure(GraphMailSettings options)
    {
        var settings = siteService.GetSettings<GraphMailSettings>();
        options.IsEnabled = settings.IsEnabled;
        options.SaveToSentItems = settings.SaveToSentItems;
        options.ConnectionKey = settings.ConnectionKey;
    }
}

public sealed class GraphMailboxSettingsConfiguration(ISiteService siteService) : IConfigureOptions<GraphMailboxSettings>
{
    public void Configure(GraphMailboxSettings options)
    {
        var settings = siteService.GetSettings<GraphMailboxSettings>();
        options.IsEnabled = settings.IsEnabled;
        options.Folder = settings.Folder;
        options.ConnectionKey = settings.ConnectionKey;
    }
}

public sealed class GraphMailProviderConfiguration(IOptionsMonitor<GraphMailSettings> settings) : IConfigureOptions<EmailProviderOptions>
{
    public void Configure(EmailProviderOptions options) =>
        options.TryAddProvider(GraphMailProvider.TechnicalName, new EmailProviderTypeOptions(typeof(GraphMailProvider))
        {
            IsEnabled = settings.CurrentValue.IsEnabled,
        });
}

public sealed class GraphMailboxProviderConfiguration(IOptionsMonitor<GraphMailboxSettings> settings) : IConfigureOptions<EmailReceiveProviderOptions>
{
    public void Configure(EmailReceiveProviderOptions options) =>
        options.TryAddProvider(GraphMailboxReceiveProvider.TechnicalName, new EmailReceiveProviderTypeOptions(typeof(GraphMailboxReceiveProvider))
        {
            IsEnabled = settings.CurrentValue.IsEnabled,
        });
}
