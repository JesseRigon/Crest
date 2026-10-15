#nullable enable
using System.Text.RegularExpressions;
using Grove.FileStorage;

namespace Grove.Media.Files;

/// <summary>
/// File bytes through one of the platform's file stores (<see cref="IFileStore"/>: Azure Blob,
/// Amazon S3), requirement 11. Quarantined uploads and committed files live under separate
/// prefixes of the store (requirement 10); keys stay opaque (content id and version). The
/// store's own base path is the tenant's root. A provider that can hand out a time-limited read
/// reference (a SAS URL, a presigned URL) passes <paramref name="temporaryReadUri"/>; it is what
/// an offsite scanner fetches (requirement 94).
/// </summary>
public sealed partial class FileStoreMediaStorageProvider(
    string name,
    IFileStore store,
    Func<string, TimeSpan, CancellationToken, Task<Uri?>>? temporaryReadUri = null) : IMediaStorageProvider
{
    public const string QuarantinePrefix = "Quarantine";

    public const string CommittedPrefix = "Committed";

    public const string DerivedPrefix = "Derived";

    public string Name { get; } = name;

    public IFileStore Store { get; } = store;

    public async Task WriteAsync(MediaStorageArea area, string key, Stream content, CancellationToken cancellationToken = default) =>
        await Store.CreateFileFromStreamAsync(PathOf(area, key), content, overwrite: true);

    public async Task<Stream?> OpenReadAsync(MediaStorageArea area, string key, CancellationToken cancellationToken = default)
    {
        var path = PathOf(area, key);
        return await Store.GetFileInfoAsync(path) is null ? null : await Store.GetFileStreamAsync(path);
    }

    public async Task CommitAsync(string key, CancellationToken cancellationToken = default)
    {
        var source = PathOf(MediaStorageArea.Quarantine, key);
        var target = PathOf(MediaStorageArea.Committed, key);
        if (await Store.GetFileInfoAsync(source) is not null)
        {
            // A store without an atomic move copies and deletes; a reader of the committed key
            // sees either nothing or the whole file.
            if (await Store.GetFileInfoAsync(target) is not null)
            {
                await Store.TryDeleteFileAsync(target);
            }

            await Store.MoveFileAsync(source, target);
        }
        else if (await Store.GetFileInfoAsync(target) is null)
        {
            throw new FileNotFoundException("There is nothing in quarantine to commit.", key);
        }
    }

    public async Task DeleteAsync(MediaStorageArea area, string key, CancellationToken cancellationToken = default) =>
        await Store.TryDeleteFileAsync(PathOf(area, key));

    public async Task DeletePrefixAsync(MediaStorageArea area, string prefix, CancellationToken cancellationToken = default) =>
        await Store.TryDeleteDirectoryAsync(PathOf(area, prefix));

    public Task<Uri?> GetTemporaryReadUriAsync(MediaStorageArea area, string key, TimeSpan lifetime, CancellationToken cancellationToken = default) =>
        temporaryReadUri is null ? Task.FromResult<Uri?>(null) : temporaryReadUri(PathOf(area, key), lifetime, cancellationToken);

    /// <summary>The store path of a key in an area, relative to the store's base path.</summary>
    public static string PathOf(MediaStorageArea area, string key)
    {
        if (!KeyPattern().IsMatch(key))
        {
            throw new ArgumentException("A storage key is an opaque id and version.", nameof(key));
        }

        var prefix = area switch
        {
            MediaStorageArea.Quarantine => QuarantinePrefix,
            MediaStorageArea.Derived => DerivedPrefix,
            _ => CommittedPrefix,
        };

        return $"{prefix}/{key}";
    }

    [GeneratedRegex("^[A-Za-z0-9_-]{1,64}(/[A-Za-z0-9_-]{1,64}){0,3}$")]
    private static partial Regex KeyPattern();
}
