using Crest.Indexing;
using Crest.Security.Permissions;

namespace Crest.Lucene;

public sealed class Permissions : IPermissionProvider
{
    private readonly IIndexProfileStore _indexStore;

    [Obsolete("This will be removed in a future release. Instead use 'LuceneSearchPermissions.ManageLuceneIndexes'.")]
    public static readonly Permission ManageLuceneIndexes = LuceneSearchPermissions.ManageLuceneIndexes;

    [Obsolete("This will be removed in a future release. Instead use 'LuceneSearchPermissions.QueryLuceneApi'.")]
    public static readonly Permission QueryLuceneApi = LuceneSearchPermissions.QueryLuceneApi;

    public Permissions(IIndexProfileStore indexStore)
    {
        _indexStore = indexStore;
    }

    public async Task<IEnumerable<Permission>> GetPermissionsAsync()
    {
        var permissions = new List<Permission>()
        {
            LuceneSearchPermissions.ManageLuceneIndexes,
            LuceneSearchPermissions.QueryLuceneApi,
        };

        var indexes = await _indexStore.GetByProviderAsync(LuceneConstants.ProviderName);

        foreach (var index in indexes)
        {
            permissions.Add(LuceneIndexPermissionHelper.GetLuceneIndexPermission(index.IndexName));
        }

        return permissions;
    }

    public IEnumerable<PermissionStereotype> GetDefaultStereotypes() =>
    [
        new PermissionStereotype
        {
            Name = PlatformConstants.Roles.Administrator,
            Permissions =
            [
                LuceneSearchPermissions.ManageLuceneIndexes,
            ],
        },
        new PermissionStereotype
        {
            Name = PlatformConstants.Roles.Editor,
            Permissions =
            [
                LuceneSearchPermissions.QueryLuceneApi,
            ],
        },
    ];
}
