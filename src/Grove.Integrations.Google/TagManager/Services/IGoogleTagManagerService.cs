using Grove.Integrations.Google.TagManager.Settings;

namespace Grove.Integrations.Google.TagManager.Services;

public interface IGoogleTagManagerService
{
    Task<GoogleTagManagerSettings> GetSettingsAsync();
}
