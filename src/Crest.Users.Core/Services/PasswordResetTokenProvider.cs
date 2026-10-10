using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Crest.Users.Models;

namespace Crest.Users.Services;

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
