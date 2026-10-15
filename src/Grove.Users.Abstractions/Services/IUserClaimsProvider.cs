using System.Security.Claims;

namespace Grove.Users.Services;

public interface IUserClaimsProvider
{
    Task GenerateAsync(IUser user, ClaimsIdentity claims);
}
