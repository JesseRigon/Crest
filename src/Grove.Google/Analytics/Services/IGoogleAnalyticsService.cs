using Grove.Google.Analytics.Settings;

namespace Grove.Google.Analytics.Services;

public interface IGoogleAnalyticsService
{
    Task<GoogleAnalyticsSettings> GetSettingsAsync();
}
