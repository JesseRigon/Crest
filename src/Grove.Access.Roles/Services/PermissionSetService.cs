using Grove.Access;
using Grove.Documents;
using Grove.Access.Roles.Models;
using Grove.Security;

namespace Grove.Access.Roles.Services;

/// <summary>
/// The permission sets store (docs/permissions.md, the permission model), beside the roles
/// document: a set write is a rights write, so it bumps the permission version and every
/// caller is rebuilt on its next request.
/// </summary>
public sealed class PermissionSetService(
    IDocumentManager<PermissionSetsDocument> documents,
    IEnumerable<IPermissionSetProvider> providers,
    IPermissionVersion permissionVersion) : IPermissionSetService
{
    public async Task<IReadOnlyList<PermissionSet>> ListAsync(CancellationToken cancellationToken = default) =>
        (await documents.GetOrCreateImmutableAsync()).Sets.Select(set => set.Clone()).OrderBy(set => set.Name, StringComparer.OrdinalIgnoreCase).ToArray();

    public async Task<PermissionSet?> FindAsync(string name, CancellationToken cancellationToken = default) =>
        (await documents.GetOrCreateImmutableAsync()).Sets.FirstOrDefault(set => string.Equals(set.Name, name, StringComparison.OrdinalIgnoreCase))?.Clone();

    public async Task SaveAsync(PermissionSet set, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(set.Name);
        var document = await documents.GetOrCreateMutableAsync();
        document.Sets.RemoveAll(existing => string.Equals(existing.Name, set.Name, StringComparison.OrdinalIgnoreCase));
        document.Sets.Add(new PermissionSet
        {
            Name = set.Name.Trim(),
            Description = string.IsNullOrWhiteSpace(set.Description) ? null : set.Description.Trim(),
            Permissions = set.Permissions.Where(permission => !string.IsNullOrWhiteSpace(permission)).Select(permission => permission.Trim()).Distinct(StringComparer.OrdinalIgnoreCase).ToList(),
        });
        await documents.UpdateAsync(document);
        await permissionVersion.BumpAsync(cancellationToken);
    }

    public async Task<bool> DeleteAsync(string name, CancellationToken cancellationToken = default)
    {
        var document = await documents.GetOrCreateMutableAsync();
        if (document.Sets.RemoveAll(existing => string.Equals(existing.Name, name, StringComparison.OrdinalIgnoreCase)) == 0)
        {
            return false;
        }

        await documents.UpdateAsync(document);
        await permissionVersion.BumpAsync(cancellationToken);
        return true;
    }

    public async Task<IReadOnlySet<string>> ExpandAsync(IEnumerable<string> setNames, CancellationToken cancellationToken = default)
    {
        var sets = (await documents.GetOrCreateImmutableAsync()).Sets;
        var permissions = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var name in setNames)
        {
            var set = sets.FirstOrDefault(candidate => string.Equals(candidate.Name, name, StringComparison.OrdinalIgnoreCase));
            if (set is not null)
            {
                permissions.UnionWith(set.Permissions);
            }
        }

        return permissions;
    }

    public async Task EnsureDefaultsAsync(CancellationToken cancellationToken = default)
    {
        var document = await documents.GetOrCreateMutableAsync();
        var added = false;
        foreach (var set in providers.SelectMany(provider => provider.GetDefaultSets()))
        {
            if (string.IsNullOrWhiteSpace(set.Name) || document.Sets.Any(existing => string.Equals(existing.Name, set.Name, StringComparison.OrdinalIgnoreCase)))
            {
                continue;
            }

            document.Sets.Add(set.Clone());
            added = true;
        }

        if (added)
        {
            await documents.UpdateAsync(document);
            await permissionVersion.BumpAsync(cancellationToken);
        }
    }
}
