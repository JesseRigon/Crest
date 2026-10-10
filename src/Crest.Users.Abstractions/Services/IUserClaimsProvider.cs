using System.Security.Claims;

namespace Crest.Users.Services;

public interface IUserClaimsProvider
{
    Task GenerateAsync(IUser user, ClaimsIdentity claims);
}
