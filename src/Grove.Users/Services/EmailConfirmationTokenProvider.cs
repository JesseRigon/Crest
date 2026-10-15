using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Grove.Users.Models;

namespace Grove.Users.Services;

public sealed class EmailConfirmationTokenProvider : DataProtectorTokenProvider<IUser>
{
    public EmailConfirmationTokenProvider(
        IDataProtectionProvider dataProtectionProvider,
        IOptions<EmailConfirmationTokenProviderOptions> options,
        ILogger<EmailConfirmationTokenProvider> logger)
        : base(dataProtectionProvider, options, logger)
    {
    }
}
