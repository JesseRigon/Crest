namespace Grove.Security;

public interface IRoleCreatedEventHandler
{
    Task RoleCreatedAsync(string roleName);
}
