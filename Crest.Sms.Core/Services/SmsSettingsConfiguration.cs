using Microsoft.Extensions.Options;
using Crest.Settings;

namespace Crest.Sms.Services;

public class SmsSettingsConfiguration : IPostConfigureOptions<SmsSettings>
{
    private readonly ISiteService _siteService;

    public SmsSettingsConfiguration(ISiteService siteService)
    {
        _siteService = siteService;
    }

    public void PostConfigure(string name, SmsSettings options)
    {
        var settings = _siteService.GetSettings<SmsSettings>();

        options.DefaultProviderName = settings.DefaultProviderName;
    }
}
