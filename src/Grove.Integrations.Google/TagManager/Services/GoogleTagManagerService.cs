using Grove.Integrations.Google.TagManager.Settings;
using Grove.Infrastructure.Settings;

namespace Grove.Integrations.Google.TagManager.Services;

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
