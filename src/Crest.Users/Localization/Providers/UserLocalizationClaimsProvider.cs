using System.Security.Claims;
using Crest.Entities;
using Crest.Users.Localization.Models;
using Crest.Users.Models;
using Crest.Users.Services;

namespace Crest.Users.Localization.Providers;

public class UserLocalizationClaimsProvider : IUserClaimsProvider
{
    internal const string CultureClaimType = "culture";

    public Task GenerateAsync(IUser user, ClaimsIdentity claims)
    {
        ArgumentNullException.ThrowIfNull(user);
        ArgumentNullException.ThrowIfNull(claims);

        if (user is not User currentUser)
        {
            return Task.CompletedTask;
        }

        if (currentUser.TryGet<UserLocalizationSettings>(out var localizationSetting) && localizationSetting.Culture != "none")
        {
            claims.AddClaim(new Claim(CultureClaimType, localizationSetting.Culture == UserLocalizationConstants.Invariant ? "" : localizationSetting.Culture));
        }

        return Task.CompletedTask;
    }
}
