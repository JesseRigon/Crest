using Crest.Security.Permissions;

namespace Crest.Elasticsearch;

public sealed class PermissionProvider : IPermissionProvider
{
    private readonly IEnumerable<Permission> _allPermissions =
    [
        ElasticsearchPermissions.ManageElasticIndexes,
        ElasticsearchPermissions.QueryElasticApi,
    ];

    public Task<IEnumerable<Permission>> GetPermissionsAsync() =>
        Task.FromResult(_allPermissions);

    public IEnumerable<PermissionStereotype> GetDefaultStereotypes() =>
    [
        new PermissionStereotype
        {
            Name = PlatformConstants.Roles.Administrator,
            Permissions =
            [
                ElasticsearchPermissions.ManageElasticIndexes,
            ],
        },
        new PermissionStereotype
        {
            Name = PlatformConstants.Roles.Editor,
            Permissions =
            [
                ElasticsearchPermissions.QueryElasticApi,
            ],
        },
    ];
}
