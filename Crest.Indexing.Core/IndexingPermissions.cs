using System.Collections.Concurrent;
using Crest.Indexing.Models;
using Crest.Security.Permissions;

namespace Crest.Indexing.Core;

public static class IndexingPermissions
{
    public static readonly Permission QuerySearchIndex = new("QuerySearchIndex", "Query any index");

    public static readonly Permission ManageIndexes = new("ManageIndexes", "Manage Indexes");

    private static readonly Permission s_indexPermissionTemplate =
        new("QueryIndex_{0}", "Query '{0}' Index", [ManageIndexes, QuerySearchIndex]);

    private static readonly ConcurrentDictionary<string, Permission> s_permissions = [];

    public static Permission CreateDynamicPermission(IndexProfile indexProfile)
    {
        ArgumentNullException.ThrowIfNull(indexProfile);

        return s_permissions.GetOrAdd(indexProfile.Id, indexId => new Permission(
            string.Format(s_indexPermissionTemplate.Name, indexProfile.Name),
            string.Format(s_indexPermissionTemplate.Description, indexProfile.Name),
            s_indexPermissionTemplate.ImpliedBy));
    }
}
