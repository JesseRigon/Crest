namespace Crest.Sharing;

/// <summary>
/// A principal a sharing picker offers (docs/sharing.md § 8, requirement 46), with its name for
/// people. A role says whether it is derived (a business role, computed from a relationship), so
/// pickers label it.
/// </summary>
public sealed record SharePrincipalOption(SharePrincipal Principal, string DisplayName, string? Description = null, bool IsDerived = false);

/// <summary>
/// Where pickers find principals and screens find their names. Sharing answers Anyone, the
/// tenant, roles (both kinds, from the role registry) and users; a module that knows another
/// kind (Parties: organizations) adds a directory for it. System accounts are never offered
/// (requirement 34).
/// </summary>
public interface ISharePrincipalDirectory
{
    /// <summary>Up to <paramref name="max"/> principals matching <paramref name="text"/> (all
    /// when empty), as the caller may see them.</summary>
    Task<IReadOnlyList<SharePrincipalOption>> SearchAsync(string? text, int max, CancellationToken cancellationToken = default);

    /// <summary>The names of the principals this directory knows among <paramref name="principals"/>.</summary>
    Task<IReadOnlyDictionary<SharePrincipal, string>> NamesAsync(IReadOnlyCollection<SharePrincipal> principals, CancellationToken cancellationToken = default);
}

/// <summary>
/// Implemented, optionally, by an <see cref="IShareableKind"/> whose objects have an address
/// people open (a file's byte URL, a content item's page): the Share panel shows it as the
/// public link while the object is public (requirement 96).
/// </summary>
public interface IShareableLinks
{
    /// <summary>The object's link, relative to the site; null when it has none.</summary>
    Task<string?> LinkAsync(string id, CancellationToken cancellationToken = default);
}
