using Grove.Data.Protocols.Https.Settings;
using Grove.Infrastructure.Settings;

namespace Grove.Data.Protocols.Https.Services;

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
