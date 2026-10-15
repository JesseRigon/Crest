using Grove.Infrastructure.Settings;
using Grove.Access.Users.Events;
using Grove.Access.Users.Models;

namespace Grove.Access.Users.Services;

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
