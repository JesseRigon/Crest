using Crest.Workflows.Studio.Login.Contracts;
using Microsoft.AspNetCore.Components;

namespace Crest.Workflows.Studio.Login.Components;

/// <summary>
/// Redirects to the login page.
/// </summary>
public class RedirectToLogin : ComponentBase
{
    /// <summary>
    /// Gets or sets the <see cref="AuthorizationService"/>.
    /// </summary>
    [Inject] protected IAuthorizationService AuthorizationService { get; set; } = default!;

    /// <inheritdoc />
    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        await AuthorizationService.RedirectToAuthorizationServer();
    }
}