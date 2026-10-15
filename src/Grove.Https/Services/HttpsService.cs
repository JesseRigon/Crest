using Grove.Https.Settings;
using Grove.Settings;

namespace Grove.Https.Services;

public class HttpsService : IHttpsService
{
    private readonly ISiteService _siteService;

    public HttpsService(ISiteService siteService)
    {
        _siteService = siteService;
    }

    public Task<HttpsSettings> GetSettingsAsync()
        => _siteService.GetSettingsAsync<HttpsSettings>();
}
