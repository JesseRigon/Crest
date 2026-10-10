namespace Crest.Security;

public interface IRoleRemovedEventHandler
{
    Task RoleRemovedAsync(string roleName);
}
