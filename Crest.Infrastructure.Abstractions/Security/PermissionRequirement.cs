using Microsoft.AspNetCore.Authorization;
using Crest.Security.Permissions;

namespace Crest.Security;

public class PermissionRequirement : IAuthorizationRequirement
{
    public PermissionRequirement(Permission permission)
    {
        ArgumentNullException.ThrowIfNull(permission);

        Permission = permission;
    }

    public Permission Permission { get; set; }
}
