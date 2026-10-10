using Crest.Security;

namespace Crest.Roles;

public interface ISystemRoleProvider
{
    IEnumerable<IRole> GetSystemRoles();

    IRole GetAdminRole();

    bool IsSystemRole(string name);
}
