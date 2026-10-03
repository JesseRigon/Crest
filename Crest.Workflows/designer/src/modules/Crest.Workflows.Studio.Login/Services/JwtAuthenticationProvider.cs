using Crest.Workflows.Studio.Contracts;
using Crest.Workflows.Studio.Login.Contracts;

namespace Crest.Workflows.Studio.Login.Services;

/// <inheritdoc />
public class JwtAuthenticationProvider(IJwtAccessor jwtAccessor) : IAuthenticationProvider
{
    /// <inheritdoc />
    public async Task<string?> GetAccessTokenAsync(string tokenName, CancellationToken cancellationToken = default)
    {
        return await jwtAccessor.ReadTokenAsync(tokenName);
    }
}