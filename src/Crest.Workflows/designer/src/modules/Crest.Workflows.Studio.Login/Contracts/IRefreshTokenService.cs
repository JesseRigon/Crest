using Crest.Workflows.Api.Client.Resources.Identity.Responses;

namespace Crest.Workflows.Studio.Login.Contracts;

/// <summary>
/// Provides a service to refresh an access_token from an authorization server
/// </summary>
public interface IRefreshTokenService
{
    /// <summary>
    /// Refreshes the access_token
    /// </summary>
    Task<LoginResponse> RefreshTokenAsync(CancellationToken cancellationToken);
}
