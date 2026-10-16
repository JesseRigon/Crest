using Grove.Communications.Email.Services;
using Grove.Entities;
using Grove.Environment.Options;
using Grove.Infrastructure.Settings;
using Microsoft.Extensions.DependencyInjection;

namespace Grove.Communications.Email;

/// <summary>
/// What a provider's section of the e-mail settings group does after a save: the default
/// provider follows the provider's enabled state, and the provider registries are refreshed.
/// </summary>
public static class EmailProviderDefaults
{
    /// <summary>
    /// A disabled send provider is no longer the default one; an enabled one becomes the default
    /// when there is none.
    /// </summary>
    public static async Task UpdateDefaultProviderAsync(IServiceProvider services, string technicalName, bool isEnabled)
    {
        var siteService = services.GetRequiredService<ISiteService>();
        var site = await siteService.LoadSiteSettingsAsync();
        var settings = site.GetOrCreate<EmailSettings>();

        if (!isEnabled && settings.DefaultProviderName == technicalName)
        {
            settings.DefaultProviderName = null;
        }
        else if (isEnabled && string.IsNullOrEmpty(settings.DefaultProviderName))
        {
            settings.DefaultProviderName = technicalName;
        }
        else
        {
            Refresh(services);
            return;
        }

        site.Put(settings);
        await siteService.UpdateSiteSettingsAsync(site);
        Refresh(services);
    }

    /// <summary>
    /// A disabled receive provider is no longer the default one; an enabled one becomes the
    /// default when there is none.
    /// </summary>
    public static async Task UpdateDefaultReceiveProviderAsync(IServiceProvider services, string technicalName, bool isEnabled)
    {
        var siteService = services.GetRequiredService<ISiteService>();
        var site = await siteService.LoadSiteSettingsAsync();
        var settings = site.GetOrCreate<EmailSettings>();

        if (!isEnabled && settings.DefaultReceiveProviderName == technicalName)
        {
            settings.DefaultReceiveProviderName = null;
        }
        else if (isEnabled && string.IsNullOrEmpty(settings.DefaultReceiveProviderName))
        {
            settings.DefaultReceiveProviderName = technicalName;
        }
        else
        {
            Refresh(services);
            return;
        }

        site.Put(settings);
        await siteService.UpdateSiteSettingsAsync(site);
        Refresh(services);
    }

    private static void Refresh(IServiceProvider services) =>
        services.GetRequiredService<IOptionsUpdateNotifier>()
            .RequestUpdate<EmailProviderOptions>()
            .RequestUpdate<EmailReceiveProviderOptions>()
            .RequestUpdate<EmailOptions>();
}
