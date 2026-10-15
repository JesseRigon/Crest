using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Grove.Users.Models;

namespace Grove.Users.Services;

public sealed class ChangeEmailTokenProvider : DataProtectorTokenProvider<IUser>
{
    public ChangeEmailTokenProvider(
        IDataProtectionProvider dataProtectionProvider,
        IOptions<ChangeEmailTokenProviderOptions> options,
        ILogger<ChangeEmailTokenProvider> logger)
        : base(dataProtectionProvider, options, logger)
    {
    }
}
