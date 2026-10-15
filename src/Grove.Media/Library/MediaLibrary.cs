#nullable enable
using Grove.Media.Files;
using Microsoft.Extensions.Caching.Memory;
using YesSql;
using YesSql.Services;

namespace Grove.Media.Library;

/// <summary>Finds the child of a folder by name.</summary>
public interface IPlacementNameLookup
{
    /// <summary>The child named <paramref name="name"/>. The folder's children belong to its
    /// content, so <paramref name="parentPlacementIds"/> are every hard placement of the folder
    /// (ruling F4).</summary>
    Task<MediaPlacementDocument?> FindChildAsync(IReadOnlyCollection<string> parentPlacementIds, string name);
}

/// <summary>One indexed query on the placement index's parent and name, through the store.</summary>
public sealed class YesSqlPlacementNameLookup(IMediaObjectStore store) : IPlacementNameLookup
{
    // The database may compare without case; the media folder on disk did not.
    public async Task<MediaPlacementDocument?> FindChildAsync(IReadOnlyCollection<string> parentPlacementIds, string name) =>
        MediaLibraryPaths.PickByName(await store.GetChildrenNamedAsync(parentPlacementIds, name), name);
}

/// <summary>Loads the folder's children and picks by name: for stores without the index (tests).</summary>
public sealed class StorePlacementNameLookup(IMediaObjectStore store) : IPlacementNameLookup
{
    public async Task<MediaPlacementDocument?> FindChildAsync(IReadOnlyCollection<string> parentPlacementIds, string name) =>
        MediaLibraryPaths.PickByName(await store.GetChildrenAsync(parentPlacementIds), name);
}

/// <summary>Library paths as segments: the same normalisation for requests, fields and templates.</summary>
public static class MediaLibraryPaths
{
    private static readonly char[] s_separators = ['/', '\\'];

    /// <summary>The segments of <paramref name="path"/>, or null when it climbs ("." or "..").</summary>
    public static string[]? Split(string? path)
    {
        var segments = (path ?? string.Empty).Split(s_separators, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return segments.Any(segment => segment is "." or "..") ? null : segments;
    }

    public static string Normalize(string? path) => string.Join('/', Split(path) ?? []);

    /// <summary>The parent path and the last segment.</summary>
    public static (string Parent, string Name) SplitLast(string path)
    {
        var normalized = Normalize(path);
        var index = normalized.LastIndexOf('/');
        return index < 0 ? (string.Empty, normalized) : (normalized[..index], normalized[(index + 1)..]);
    }

    internal static MediaPlacementDocument? PickByName(IEnumerable<MediaPlacementDocument> candidates, string name) =>
        candidates
            .Where(candidate => string.Equals(candidate.Placement.Name, name, StringComparison.Ordinal))
            .OrderBy(candidate => candidate.Placement.IsSoftLink ? 1 : 0)
            .ThenBy(candidate => candidate.Placement.Id, StringComparer.Ordinal)
            .FirstOrDefault();
}

/// <summary>
/// The media library drive and its paths (ruling F1). Lookups read the store directly and decide
/// nothing: whoever opens the placement goes through <see cref="IMediaObjectService"/>. Resolved
/// paths are cached for a short while and checked against the placement on every hit, so a
/// rename or delete is seen at once and a move within <see cref="CacheLifetime"/>.
/// </summary>
public sealed class MediaLibrary(
    IMediaObjectStore store,
    IPlacementNameLookup names,
    IMediaLibraryState state,
    IMemoryCache cache) : IMediaLibrary
{
    public static readonly TimeSpan CacheLifetime = TimeSpan.FromSeconds(30);

    private const int MaxDepth = 256;

    public async Task<string?> GetDriveIdAsync(CancellationToken cancellationToken = default) =>
        (await state.GetAsync()).DrivePlacementId;

    public async Task<bool> IsActiveAsync(CancellationToken cancellationToken = default)
    {
        var document = await state.GetAsync();
        return document.ImportedUtc is not null && !string.IsNullOrEmpty(document.DrivePlacementId);
    }

    public async Task<string?> ResolvePathAsync(string path, CancellationToken cancellationToken = default)
    {
        var driveId = await GetDriveIdAsync(cancellationToken);
        var segments = MediaLibraryPaths.Split(path);
        if (driveId is null || segments is null)
        {
            return null;
        }

        if (segments.Length == 0)
        {
            return driveId;
        }

        var key = $"media-library-path:{driveId}:{string.Join('/', segments)}";
        if (cache.TryGetValue<string>(key, out var cached) && cached is not null)
        {
            var hit = await store.GetPlacementAsync(cached);
            if (hit is not null && hit.Placement.DriveId == driveId && string.Equals(hit.Placement.Name, segments[^1], StringComparison.Ordinal))
            {
                return cached;
            }

            cache.Remove(key);
        }

        var current = await store.GetPlacementAsync(driveId);
        foreach (var segment in segments)
        {
            if (current is null)
            {
                return null;
            }

            // A soft link on the way opens its target; the target's children are the folder's.
            var folder = current.Placement.LinkTargetPlacementId is { } target ? await store.GetPlacementAsync(target) : current;
            if (folder is null)
            {
                return null;
            }

            var holders = await store.GetPlacementsOfContentAsync(folder.Placement.ContentId);
            current = await names.FindChildAsync([.. holders.Select(holder => holder.Placement.Id)], segment);
        }

        if (current is null)
        {
            return null;
        }

        cache.Set(key, current.Placement.Id, CacheLifetime);
        return current.Placement.Id;
    }

    public async Task<string?> GetPathAsync(string placementId, CancellationToken cancellationToken = default)
    {
        var driveId = await GetDriveIdAsync(cancellationToken);
        if (driveId is null || string.IsNullOrEmpty(placementId))
        {
            return null;
        }

        var names = new List<string>();
        var current = await store.GetPlacementAsync(placementId);
        if (current is null || current.Placement.DriveId != driveId)
        {
            return null;
        }

        for (var depth = 0; current is not null && depth < MaxDepth; depth++)
        {
            if (current.Placement.ParentId is not { } parentId)
            {
                if (current.Placement.Id != driveId)
                {
                    return null;
                }

                names.Reverse();
                return string.Join('/', names);
            }

            names.Add(current.Placement.Name);
            current = await store.GetPlacementAsync(parentId);
        }

        return null;
    }
}
