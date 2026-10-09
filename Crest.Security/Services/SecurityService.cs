using Crest.Security.Settings;
using Crest.Settings;

namespace Crest.Security.Services;

public class SecurityService : ISecurityService
{
    private readonly ISiteService _siteService;

    public SecurityService(ISiteService siteService)
    {
        _siteService = siteService;
    }

    public Task<SecuritySettings> GetSettingsAsync()
        => _siteService.GetSettingsAsync<SecuritySettings>();
}
