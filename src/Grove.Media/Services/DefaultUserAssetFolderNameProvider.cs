using System.Security.Claims;

namespace Grove.Media.Services;

public class DefaultUserAssetFolderNameProvider : IUserAssetFolderNameProvider
{
    public string GetUserAssetFolderName(ClaimsPrincipal claimsPrincipal)
    {
        return claimsPrincipal.FindFirstValue(ClaimTypes.NameIdentifier);
    }
}
