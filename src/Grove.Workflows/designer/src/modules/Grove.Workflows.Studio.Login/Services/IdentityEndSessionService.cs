using Grove.Workflows.Studio.Login.Contracts;
using Microsoft.AspNetCore.Components;

namespace Grove.Workflows.Studio.Login.Services;

///<inheritdoc/>
public class IdentityEndSessionService(IJwtAccessor jwtAccessor, NavigationManager navigationManager) : IEndSessionService
{
    ///<inheritdoc/>
    public async Task LogoutAsync()
    {
        await jwtAccessor.WriteTokenAsync(TokenNames.AccessToken, "");
        await jwtAccessor.WriteTokenAsync(TokenNames.RefreshToken, "");
        navigationManager.NavigateTo("/", true);
    }
}
