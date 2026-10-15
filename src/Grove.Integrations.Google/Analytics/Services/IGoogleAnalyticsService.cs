using Grove.Integrations.Google.Analytics.Settings;

namespace Grove.Integrations.Google.Analytics.Services;

public interface IGoogleAnalyticsService
{
    Task<GoogleAnalyticsSettings> GetSettingsAsync();
}
