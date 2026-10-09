namespace Crest.Users.Services;

public interface IUserIdGenerator
{
    string GenerateUniqueId(IUser user);
}
