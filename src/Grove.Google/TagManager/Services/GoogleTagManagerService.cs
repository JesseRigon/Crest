using Grove.Google.TagManager.Settings;
using Grove.Settings;

namespace Grove.Google.TagManager.Services;

public class GoogleTagManagerService : IGoogleTagManagerService
{
    private readonly ISiteService _siteService;

    public GoogleTagManagerService(ISiteService siteService)
    {
        _siteService = siteService;
    }

    public Task<GoogleTagManagerSettings> GetSettingsAsync()
        => _siteService.GetSettingsAsync<GoogleTagManagerSettings>();
}
