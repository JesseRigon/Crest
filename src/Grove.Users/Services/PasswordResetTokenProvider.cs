using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Grove.Users.Models;

namespace Grove.Users.Services;

public sealed class PasswordResetTokenProvider : DataProtectorTokenProvider<IUser>
{
    public PasswordResetTokenProvider(
        IDataProtectionProvider dataProtectionProvider,
        IOptions<PasswordResetTokenProviderOptions> options,
        ILogger<PasswordResetTokenProvider> logger)
        : base(dataProtectionProvider, options, logger)
    {
    }
}
