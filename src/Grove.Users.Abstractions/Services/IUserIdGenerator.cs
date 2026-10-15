namespace Grove.Users.Services;

public interface IUserIdGenerator
{
    string GenerateUniqueId(IUser user);
}
