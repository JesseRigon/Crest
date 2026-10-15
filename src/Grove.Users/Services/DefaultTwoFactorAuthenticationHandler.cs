using Grove.Settings;
using Grove.Users.Events;
using Grove.Users.Models;

namespace Grove.Users.Services;

public class DefaultTwoFactorAuthenticationHandler : ITwoFactorAuthenticationHandler
{
    private readonly ISiteService _siteService;

    public DefaultTwoFactorAuthenticationHandler(ISiteService siteService)
    {
        _siteService = siteService;
    }

    public async Task<bool> IsRequiredAsync(IUser user)
        => (await _siteService.GetSettingsAsync<TwoFactorLoginSettings>()).RequireTwoFactorAuthentication;
}
