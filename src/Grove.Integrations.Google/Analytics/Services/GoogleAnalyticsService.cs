using Grove.Integrations.Google.Analytics.Settings;
using Grove.Infrastructure.Settings;

namespace Grove.Integrations.Google.Analytics.Services;

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
