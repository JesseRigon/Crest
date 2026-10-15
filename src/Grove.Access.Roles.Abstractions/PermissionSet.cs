namespace Grove.Security;

/// <summary>
/// A tenant-defined named list of permissions (docs/permissions.md, the permission model):
/// the layer between a permission and a role. Roles hold sets by name; modules ship defaults
/// through <see cref="IPermissionSetProvider"/>, which the tenant edits afterwards.
/// </summary>
public class PermissionSet
{
    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }

    public List<string> Permissions { get; set; } = [];

    public PermissionSet Clone() => new()
    {
        Name = Name,
        Description = Description,
        Permissions = [.. Permissions],
    };
}

/// <summary>A module's default permission sets, created once when missing and then the tenant's.</summary>
public interface IPermissionSetProvider
{
    IEnumerable<PermissionSet> GetDefaultSets();
}

/// <summary>The tenant's permission sets: the schema organizations reference and stay synced with.</summary>
public interface IPermissionSetService
{
    Task<IReadOnlyList<PermissionSet>> ListAsync(CancellationToken cancellationToken = default);

    Task<PermissionSet?> FindAsync(string name, CancellationToken cancellationToken = default);

    /// <summary>Creates or replaces the set named <paramref name="set"/>.Name.</summary>
    Task SaveAsync(PermissionSet set, CancellationToken cancellationToken = default);

    Task<bool> DeleteAsync(string name, CancellationToken cancellationToken = default);

    /// <summary>The permission names the named sets grant together; an unknown set grants nothing.</summary>
    Task<IReadOnlySet<string>> ExpandAsync(IEnumerable<string> setNames, CancellationToken cancellationToken = default);

    /// <summary>Creates every provider's default set whose name is not taken.</summary>
    Task EnsureDefaultsAsync(CancellationToken cancellationToken = default);
}
