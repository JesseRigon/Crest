namespace Crest.Security;

public interface IRoleCreatedEventHandler
{
    Task RoleCreatedAsync(string roleName);
}
