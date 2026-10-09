using Crest.Google.TagManager.Settings;

namespace Crest.Google.TagManager.Services;

public interface IGoogleTagManagerService
{
    Task<GoogleTagManagerSettings> GetSettingsAsync();
}
