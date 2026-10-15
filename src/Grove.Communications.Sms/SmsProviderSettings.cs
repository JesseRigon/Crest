using Microsoft.Extensions.DependencyInjection;
using Grove.Entities;
using Grove.Infrastructure.Settings;
using Grove.Validation;

namespace Grove.Communications.Sms;

/// <summary>
/// The rules a provider's section of the SMS settings group shares: a sender phone number, and
/// the default provider following the provider's enabled state.
/// </summary>
public static class SmsProviderSettings
{
    /// <summary>Requires a valid phone number under <paramref name="member"/>.</summary>
    public static void ValidatePhoneNumber(IServiceProvider services, string phoneNumber, string member, ValidationErrors errors)
    {
        if (string.IsNullOrWhiteSpace(phoneNumber))
        {
            errors.Add(member, "Phone number requires a value.");
        }
        else if (!services.GetRequiredService<IPhoneFormatValidator>().IsValid(phoneNumber))
        {
            errors.Add(member, "Please provide a valid phone number.");
        }
    }

    /// <summary>
    /// After a provider's settings were saved: a disabled provider is no longer the default one,
    /// an enabled provider becomes the default one when there is none.
    /// </summary>
    public static async Task UpdateDefaultProviderAsync(IServiceProvider services, string technicalName, bool isEnabled)
    {
        var siteService = services.GetRequiredService<ISiteService>();
        var site = await siteService.LoadSiteSettingsAsync();
        var smsSettings = site.GetOrCreate<SmsSettings>();

        string defaultProviderName;
        if (!isEnabled && smsSettings.DefaultProviderName == technicalName)
        {
            defaultProviderName = null;
        }
        else if (isEnabled && string.IsNullOrEmpty(smsSettings.DefaultProviderName))
        {
            defaultProviderName = technicalName;
        }
        else
        {
            return;
        }

        smsSettings.DefaultProviderName = defaultProviderName;
        site.Put(smsSettings);
        await siteService.UpdateSiteSettingsAsync(site);
    }
}
