using Microsoft.Extensions.Caching.Memory;

namespace Crest.Access.Services;

/// <summary>
/// What the caller is built from, cached per (tenant, user, side, organization) under the
/// permission version. Singleton, bounded by expiry: an entry lives thirty minutes since its
/// last use and twelve hours at most; a version bump makes every entry stale at once. Only
/// server-held, user-level facts are cached (roles, permissions, class, the validated
/// organization); request-level facts (the impersonator) never enter it.
/// </summary>
public sealed class CallerStateCache(IMemoryCache cache)
{
    private static readonly MemoryCacheEntryOptions s_options = new()
    {
        SlidingExpiration = TimeSpan.FromMinutes(30),
        AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(12),
    };

    public bool TryGet(string key, long version, out CallerState state)
    {
        if (cache.TryGetValue(key, out CallerState? found) && found is not null && found.PermissionVersion == version)
        {
            state = found;
            return true;
        }

        state = null!;
        return false;
    }

    public void Set(string key, CallerState state) => cache.Set(key, state, s_options);

    /// <summary>An anonymous caller has no organization, so a requested one never reaches the
    /// key: an unauthenticated client cannot grow the cache by varying a header.</summary>
    public static string Key(string tenant, string? userId, CallerSide side, string? organizationId) =>
        string.Join('\u001f', "Crest.Access.Caller", tenant, userId ?? string.Empty, side.ToString(), userId is null ? string.Empty : organizationId ?? string.Empty);
}

public sealed record CallerState(
    long PermissionVersion,
    string? UserName,
    bool IsSuperUser,
    IReadOnlySet<string> Roles,
    IReadOnlySet<string> Permissions,
    string? OrganizationId,
    string? UserClass);
