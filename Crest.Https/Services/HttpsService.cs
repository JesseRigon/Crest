using Crest.Https.Settings;
using Crest.Settings;

namespace Crest.Https.Services;

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
