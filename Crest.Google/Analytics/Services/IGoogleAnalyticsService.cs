using Crest.Google.Analytics.Settings;

namespace Crest.Google.Analytics.Services;

public interface IGoogleAnalyticsService
{
    Task<GoogleAnalyticsSettings> GetSettingsAsync();
}
