using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using Grove.Users.Models;

namespace Grove.Users.Services;

public sealed class PasswordResetIdentityOptionsConfigurations : IConfigureOptions<IdentityOptions>
{
    private readonly PasswordResetTokenProviderOptions _tokenOptions;

    public PasswordResetIdentityOptionsConfigurations(IOptions<PasswordResetTokenProviderOptions> tokenOptions)
    {
        _tokenOptions = tokenOptions.Value;
    }

    public void Configure(IdentityOptions options)
    {
        options.Tokens.PasswordResetTokenProvider = _tokenOptions.Name;
        options.Tokens.ProviderMap[_tokenOptions.Name] = new TokenProviderDescriptor(typeof(PasswordResetTokenProvider));
    }
}
