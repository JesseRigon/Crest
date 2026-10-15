using System.Security.Claims;

namespace Grove.Access.Users.Services;

public interface IUserClaimsProvider
{
    Task GenerateAsync(IUser user, ClaimsIdentity claims);
}
