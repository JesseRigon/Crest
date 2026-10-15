using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Grove.Access.Users.Models;

namespace Grove.Access.Users.Services;

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
