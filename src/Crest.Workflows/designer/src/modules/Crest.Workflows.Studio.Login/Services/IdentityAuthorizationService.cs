using Crest.Workflows.Studio.Login.Contracts;
using Microsoft.AspNetCore.Components;

namespace Crest.Workflows.Studio.Login.Services;

/// <inheritdoc/>
internal class IdentityAuthorizationService(NavigationManager navigationManager) : IAuthorizationService
{
    /// <inheritdoc/>
    public Task RedirectToAuthorizationServer()
    {
        var returnUrl = navigationManager.ToBaseRelativePath(navigationManager.Uri);
        var loginUrl = string.IsNullOrWhiteSpace(returnUrl) ? "/login" : $"/login?returnUrl={returnUrl}";
        navigationManager.NavigateTo(loginUrl, true);

        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public Task ReceiveAuthorizationCode(string code, string? state, CancellationToken cancellationToken)
    {
        throw new NotSupportedException();
    }
}
