using Microsoft.AspNetCore.Authorization;
using Grove.Security.Permissions;

namespace Grove.Security;

public class PermissionRequirement : IAuthorizationRequirement
{
    public PermissionRequirement(Permission permission)
    {
        ArgumentNullException.ThrowIfNull(permission);

        Permission = permission;
    }

    public Permission Permission { get; set; }
}
