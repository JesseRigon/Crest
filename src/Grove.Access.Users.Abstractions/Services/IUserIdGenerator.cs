namespace Grove.Access.Users.Services;

public interface IUserIdGenerator
{
    string GenerateUniqueId(IUser user);
}
