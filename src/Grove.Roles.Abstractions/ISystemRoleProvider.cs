using Grove.Security;

namespace Grove.Roles;

public interface ISystemRoleProvider
{
    IEnumerable<IRole> GetSystemRoles();

    IRole GetAdminRole();

    bool IsSystemRole(string name);
}
