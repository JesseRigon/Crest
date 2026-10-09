using System.Security.Claims;

namespace Crest.Media;

public interface IUserAssetFolderNameProvider
{
    string GetUserAssetFolderName(ClaimsPrincipal claimsPrincipal);
}
