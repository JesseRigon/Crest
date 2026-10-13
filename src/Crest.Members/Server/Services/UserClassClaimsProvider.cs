using System.Security.Claims;
using Crest.Members.Constants;
using Crest.Members.Models;
using Crest.Entities;
using Crest.Users;
using Crest.Users.Services;
using Crest.Users.Models;

namespace Crest.Members.Services;

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
