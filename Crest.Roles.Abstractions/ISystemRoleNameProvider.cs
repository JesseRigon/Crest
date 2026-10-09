using System.Collections.Frozen;

namespace Crest.Roles;

[Obsolete("This interface has been deprecated, use ISystemRoleProvider instead.")]
public interface ISystemRoleNameProvider
{
    ValueTask<string> GetAdminRoleAsync();

    ValueTask<FrozenSet<string>> GetSystemRolesAsync();
}
