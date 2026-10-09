using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using Crest.Users.Models;

namespace Crest.Users.Services;

public sealed class EmailConfirmationIdentityOptionsConfigurations : IConfigureOptions<IdentityOptions>
{
    private readonly EmailConfirmationTokenProviderOptions _tokenOptions;

    public EmailConfirmationIdentityOptionsConfigurations(IOptions<EmailConfirmationTokenProviderOptions> tokenOptions)
    {
        _tokenOptions = tokenOptions.Value;
    }

    public void Configure(IdentityOptions options)
    {
        options.Tokens.EmailConfirmationTokenProvider = _tokenOptions.Name;
        options.Tokens.ProviderMap[_tokenOptions.Name] = new TokenProviderDescriptor(typeof(EmailConfirmationTokenProvider));
    }
}
