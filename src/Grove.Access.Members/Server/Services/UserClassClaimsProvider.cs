using System.Security.Claims;
using Grove.Access.Members.Constants;
using Grove.Access.Members.Models;
using Grove.Entities;
using Grove.Access.Users;
using Grove.Access.Users.Services;
using Grove.Access.Users.Models;

namespace Grove.Access.Members.Services;

/// <summary>
/// Adds the class claims at principal creation (sign-in / principal refresh), so the
/// permission ceiling and route/login gates read the PRINCIPAL - no store hit per
/// authorization check. Collected by stock DefaultUserClaimsPrincipalProviderFactory.
/// </summary>
public class UserClassClaimsProvider : IUserClaimsProvider
{
    public Task GenerateAsync(IUser user, ClaimsIdentity claims)
    {
        // The class is a set (a user may be staff and a member): one claim per class.
        if (user is User localUser)
        {
            foreach (var userClass in UserClassSet.Classes(localUser))
            {
                claims.AddClaim(new Claim(MemberClaims.UserClass, userClass));
            }
        }

        return Task.CompletedTask;
    }
}
