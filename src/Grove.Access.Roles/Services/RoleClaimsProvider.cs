using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using Grove.Security;
using Grove.Access.Users;
using Grove.Access.Users.Services;

namespace Grove.Access.Roles;

public class RoleClaimsProvider : IUserClaimsProvider
{
    private readonly UserManager<IUser> _userManager;
    private readonly IdentityOptions _identityOptions;

    public RoleClaimsProvider(
        UserManager<IUser> userManager,
        IOptions<IdentityOptions> identityOptions)
    {
        _userManager = userManager;
        _identityOptions = identityOptions.Value;
    }

    public async Task GenerateAsync(IUser user, ClaimsIdentity claims)
    {
        if (!_userManager.SupportsUserRole)
        {
            return;
        }

        // Roles only: a Permission claim per role used to be stamped here, which made the
        // cookie a second permission store. The access gate's caller factory reads the
        // roles' permissions from the role store under the permission version (docs/access.md).
        foreach (var roleName in await _userManager.GetRolesAsync(user))
        {
            claims.AddClaim(new Claim(_identityOptions.ClaimsIdentity.RoleClaimType, roleName));
        }
    }
}
