using System.Security.Claims;

namespace Crest.Media;

public interface IUserAssetFolderNameProvider
{
    string GetUserAssetFolderName(ClaimsPrincipal claimsPrincipal);

    /// <summary>The folder name for a user id, as the access decision asks it (the request's caller, never the principal).</summary>
    string? GetUserAssetFolderName(string? userId) => userId;
}
