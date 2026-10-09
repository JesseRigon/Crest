using Crest.Google.Analytics.Settings;
using Crest.Settings;

namespace Crest.Google.Analytics.Services;

public class GoogleAnalyticsService : IGoogleAnalyticsService
{
    private readonly ISiteService _siteService;

    public GoogleAnalyticsService(
        ISiteService siteService)
    {
        _siteService = siteService;
    }

    public Task<GoogleAnalyticsSettings> GetSettingsAsync()
        => _siteService.GetSettingsAsync<GoogleAnalyticsSettings>();
}
