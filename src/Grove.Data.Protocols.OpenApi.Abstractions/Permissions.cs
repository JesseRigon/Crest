using System.Collections.Generic;
using System.Threading.Tasks;
using Grove.Security.Permissions;

namespace Grove.Data.Protocols.OpenApi;

public sealed class Permissions : IPermissionProvider
{
    private readonly IEnumerable<Permission> _allPermissions =
    [
        OpenApiPermissions.ManageOpenApi,
        OpenApiPermissions.ViewOpenApiContent,
    ];

    public Task<IEnumerable<Permission>> GetPermissionsAsync() => Task.FromResult(_allPermissions);

    public IEnumerable<PermissionStereotype> GetDefaultStereotypes() =>
        [
            new PermissionStereotype
            {
                Name = PlatformConstants.Roles.Administrator,
                Permissions = _allPermissions,
            },
        ];
}
