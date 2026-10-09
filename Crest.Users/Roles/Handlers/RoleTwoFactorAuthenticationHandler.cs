using Microsoft.AspNetCore.Identity;
using Crest.Settings;
using Crest.Users.Events;
using Crest.Users.Models;

namespace Crest.Users.Services;

public class RoleTwoFactorAuthenticationHandler : ITwoFactorAuthenticationHandler
{
    private readonly UserManager<IUser> _userManager;
    private readonly ISiteService _siteService;

    public RoleTwoFactorAuthenticationHandler(
        UserManager<IUser> userManager,
        ISiteService siteService)
    {
        _userManager = userManager;
        _siteService = siteService;
    }

    public async Task<bool> IsRequiredAsync(IUser user)
    {
        ArgumentNullException.ThrowIfNull(user);

        var loginSettings = await _siteService.GetSettingsAsync<RoleLoginSettings>();

        if (loginSettings.RequireTwoFactorAuthenticationForSpecificRoles && loginSettings.Roles != null)
        {
            foreach (var role in loginSettings.Roles)
            {
                if (await _userManager.IsInRoleAsync(user, role))
                {
                    return true;
                }
            }
        }

        return false;
    }
}
