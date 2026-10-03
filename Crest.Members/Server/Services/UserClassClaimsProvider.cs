using System.Security.Claims;
using Crest.Members.Constants;
using Crest.Members.Models;
using OrchardCore.Entities;
using OrchardCore.Users;
using OrchardCore.Users.Services;
using OrchardCore.Users.Models;

namespace Crest.Members.Services;

/// <summary>
/// Adds the class claim at principal creation (sign-in / principal refresh), so the
/// permission ceiling and route/login gates read the PRINCIPAL - no store hit per
/// authorization check. Collected by stock DefaultUserClaimsPrincipalProviderFactory.
/// </summary>
public class UserClassClaimsProvider : IUserClaimsProvider
{
    public Task GenerateAsync(IUser user, ClaimsIdentity claims)
    {
        var userClass = user is User localUser && localUser.TryGet<CrestUserClass>(out var stamped)
            ? stamped.Class
            : UserClasses.Staff;

        claims.AddClaim(new Claim(MemberClaims.UserClass, userClass));

        return Task.CompletedTask;
    }
}
