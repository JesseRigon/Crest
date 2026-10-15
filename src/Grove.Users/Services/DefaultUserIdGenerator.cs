using Grove.Entities;

namespace Grove.Users.Services;

public class DefaultUserIdGenerator : IUserIdGenerator
{
    private readonly IIdGenerator _generator;

    public DefaultUserIdGenerator(IIdGenerator generator)
    {
        _generator = generator;
    }

    public string GenerateUniqueId(IUser user)
    {
        return _generator.GenerateUniqueId();
    }
}
