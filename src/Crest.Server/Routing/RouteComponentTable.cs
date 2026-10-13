using Crest.Services;
using Microsoft.AspNetCore.Http;

namespace Crest.Routing;

// The built artifact: read-only once constructed, one instance per (active Admin theme id,
// active Site theme id) pair for a tenant - see DefaultRouteComponentTableManager for the
// caching/keying policy. Matching reuses the Access route gate's existing
// segment-tokenized template grammar rather than inventing a second one.
public sealed class RouteComponentTable(IReadOnlyList<RouteComponentEntry> entries)
{
    public IReadOnlyList<RouteComponentEntry> Entries { get; } = entries;

    // Matches within one shell only. The same literal exists in several shells
    // ("/" in admin, site and member; "/login" in admin and member), so a first match
    // across the whole table would find another shell's page and hide this shell's own.
    public bool TryMatch(PathString path, ShellId shell, out RouteComponentEntry entry) =>
        TryMatch(path, shell, _ => true, out entry);

    /// <summary>Matches within one shell, among the entries <paramref name="filter"/> admits.</summary>
    public bool TryMatch(PathString path, ShellId shell, Func<RouteComponentEntry, bool> filter, out RouteComponentEntry entry)
    {
        foreach (var candidate in Entries)
        {
            if (candidate.Shell == shell && filter(candidate) && Crest.Access.RouteTemplates.Matches(candidate.RoutePattern, path.Value))
            {
                entry = candidate;
                return true;
            }
        }

        entry = null!;
        return false;
    }

    public RouteComponentEntry? DefaultLanding(ShellId shell) =>
        Entries.FirstOrDefault(candidate => candidate.IsDefaultLanding && candidate.Shell == shell);
}
