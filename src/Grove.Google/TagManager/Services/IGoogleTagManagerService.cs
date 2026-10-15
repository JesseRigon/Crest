using Grove.Google.TagManager.Settings;

namespace Grove.Google.TagManager.Services;

public interface IGoogleTagManagerService
{
    Task<GoogleTagManagerSettings> GetSettingsAsync();
}
