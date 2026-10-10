using Microsoft.AspNetCore.Identity;

namespace Crest.Users.Models;

public sealed class ChangeEmailTokenProviderOptions : DataProtectionTokenProviderOptions
{
    public ChangeEmailTokenProviderOptions()
    {
        Name = "ChangeEmailDataProtectionTokenProvider";
        TokenLifespan = TimeSpan.FromMinutes(15);
    }
}
