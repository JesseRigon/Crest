#nullable enable
using System.Text.RegularExpressions;

namespace Crest.Media.Files;

/// <summary>
/// File bytes on local disk (requirement 11), in the tenant's own root
/// (<c>App_Data/Sites/&lt;tenant&gt;/Files</c>), apart from the media library's <c>Media</c>
/// folder, so nothing that serves <c>/media</c> reaches them. Quarantined uploads and committed
/// files are separate directories (requirement 10). Keys are opaque (content id and version).
/// </summary>
public sealed partial class LocalMediaStorageProvider(string root) : IMediaStorageProvider
{
    public string Name => "local";

    public string Root { get; } = Path.GetFullPath(root);

    public async Task WriteAsync(MediaStorageArea area, string key, Stream content, CancellationToken cancellationToken = default)
    {
        var path = PathOf(area, key);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);

        // Written beside the target and moved into place, so a reader never sees half a file.
        var temporary = $"{path}.{Guid.NewGuid():n}.tmp";
        try
        {
            await using (var file = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None, 81920, useAsync: true))
            {
                await content.CopyToAsync(file, cancellationToken);
            }

            File.Move(temporary, path, overwrite: true);
        }
        finally
        {
            if (File.Exists(temporary))
            {
                File.Delete(temporary);
            }
        }
    }

    public Task<Stream?> OpenReadAsync(MediaStorageArea area, string key, CancellationToken cancellationToken = default)
    {
        var path = PathOf(area, key);
        return Task.FromResult<Stream?>(File.Exists(path)
            ? new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 81920, useAsync: true)
            : null);
    }

    public Task CommitAsync(string key, CancellationToken cancellationToken = default)
    {
        var source = PathOf(MediaStorageArea.Quarantine, key);
        var target = PathOf(MediaStorageArea.Committed, key);
        if (File.Exists(source))
        {
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            File.Move(source, target, overwrite: true);
        }
        else if (!File.Exists(target))
        {
            throw new FileNotFoundException("There is nothing in quarantine to commit.", key);
        }

        return Task.CompletedTask;
    }

    public Task DeleteAsync(MediaStorageArea area, string key, CancellationToken cancellationToken = default)
    {
        var path = PathOf(area, key);
        if (File.Exists(path))
        {
            File.Delete(path);
        }

        return Task.CompletedTask;
    }

    public Task DeletePrefixAsync(MediaStorageArea area, string prefix, CancellationToken cancellationToken = default)
    {
        var path = PathOf(area, prefix);
        if (Directory.Exists(path))
        {
            Directory.Delete(path, recursive: true);
        }

        return Task.CompletedTask;
    }

    public Task<Uri?> GetTemporaryReadUriAsync(MediaStorageArea area, string key, TimeSpan lifetime, CancellationToken cancellationToken = default) =>
        Task.FromResult<Uri?>(null);

    private string PathOf(MediaStorageArea area, string key)
    {
        if (!KeyPattern().IsMatch(key))
        {
            throw new ArgumentException("A storage key is an opaque id and version.", nameof(key));
        }

        var directory = area switch
        {
            MediaStorageArea.Quarantine => "Quarantine",
            MediaStorageArea.Derived => "Derived",
            _ => "Committed",
        };
        var path = Path.GetFullPath(Path.Combine(Root, directory, key.Replace('/', Path.DirectorySeparatorChar)));
        if (!path.StartsWith(Root + Path.DirectorySeparatorChar, StringComparison.Ordinal))
        {
            throw new ArgumentException("A storage key stays in the tenant's root.", nameof(key));
        }

        return path;
    }

    [GeneratedRegex("^[A-Za-z0-9_-]{1,64}(/[A-Za-z0-9_-]{1,64}){0,3}$")]
    private static partial Regex KeyPattern();
}
