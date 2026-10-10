using Crest.Workflows.Studio.Login.Contracts;
using Microsoft.AspNetCore.Components;

namespace Crest.Workflows.Studio.Login.Services;

///<inheritdoc/>
public class CrestWorkflowsIdentityEndSessionService(IJwtAccessor jwtAccessor, NavigationManager navigationManager) : IEndSessionService
{
    ///<inheritdoc/>
    public async Task LogoutAsync()
    {
        await jwtAccessor.WriteTokenAsync(TokenNames.AccessToken, "");
        await jwtAccessor.WriteTokenAsync(TokenNames.RefreshToken, "");
        navigationManager.NavigateTo("/", true);
    }
}
