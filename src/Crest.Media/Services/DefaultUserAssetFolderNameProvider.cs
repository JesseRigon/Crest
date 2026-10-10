using System.Security.Claims;

namespace Crest.Media.Services;

public class DefaultUserAssetFolderNameProvider : IUserAssetFolderNameProvider
{
    public string GetUserAssetFolderName(ClaimsPrincipal claimsPrincipal)
    {
        return claimsPrincipal.FindFirstValue(ClaimTypes.NameIdentifier);
    }
}
