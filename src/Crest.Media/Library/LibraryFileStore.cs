#nullable enable
using Crest.Environment.Shell.Scope;
using Crest.FileStorage;
using Crest.Media.Files;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.Extensions.DependencyInjection;

namespace Crest.Media.Library;

/// <summary>
/// <see cref="IMediaFileStore"/> paths over the media library drive (ruling F1): the store under
/// <see cref="DefaultMediaFileStore"/>, so every module that works with media paths (the media
/// admin and its API, recipes, deployment, attached media fields, SEO) works on file objects.
/// <list type="bullet">
/// <item>Until the library is active (its import finished) every call goes to the media folder on
/// disk, as before.</item>
/// <item>Reads are path lookups, as the folder on disk was: the surfaces that call the store
/// decide first (the media API by path, through sharing's "media" kind or the media folder
/// permissions). They read the store directly, so the path permission mappers that read it too
/// never re-enter the decision.</item>
/// <item>Writes go through <see cref="IMediaObjectService"/> as the caller (the system actor for
/// server-side work with none): uploads land in quarantine and are served once clean, an
/// overwrite adds a version, a delete or a move is checked on the placement.</item>
/// </list>
/// Singleton, like the store it backs; the scoped services come from the current shell scope.
/// </summary>
public sealed class LibraryFileStore(IFileStore legacy) : IFileStore
{
    private static readonly FileExtensionContentTypeProvider s_contentTypes = new();

    /// <summary>The media folder on disk: the import reads it, and it serves until the library is active.</summary>
    public IFileStore Legacy { get; } = legacy;

    public IFileStoreCapabilities Capabilities { get; } = new FileStoreCapabilities(hasHierarchicalNamespace: true, supportsAtomicMove: true);

    public string StorageName => "Media library";

    public async Task<IFileStoreEntry> GetFileInfoAsync(string path)
    {
        if (await ScopeAsync() is not { } scope)
        {
            return await Legacy.GetFileInfoAsync(path);
        }

        var found = await scope.FindAsync(path);
        return found is { Content.Content.Kind: MediaObjectKind.File } ? Entry(MediaLibraryPaths.Normalize(path), found.Value) : null!;
    }

    public async Task<IFileStoreEntry> GetDirectoryInfoAsync(string path)
    {
        if (await ScopeAsync() is not { } scope)
        {
            return await Legacy.GetDirectoryInfoAsync(path);
        }

        var found = await scope.FindAsync(path);
        return found is { Content.Content.Kind: not MediaObjectKind.File } ? Entry(MediaLibraryPaths.Normalize(path), found.Value) : null!;
    }

    public async IAsyncEnumerable<IFileStoreEntry> GetDirectoryContentAsync(string? path = null, bool includeSubDirectories = false)
    {
        if (await ScopeAsync() is not { } scope)
        {
            await foreach (var entry in Legacy.GetDirectoryContentAsync(path, includeSubDirectories))
            {
                yield return entry;
            }

            yield break;
        }

        var root = MediaLibraryPaths.Normalize(path);
        var folder = await scope.FindAsync(root);
        if (folder is not { Content.Content.Kind: not MediaObjectKind.File })
        {
            yield break;
        }

        var pending = new Queue<(string Path, Found Folder, int Depth)>();
        pending.Enqueue((root, folder.Value, 0));
        var directories = new List<IFileStoreEntry>();
        var files = new List<IFileStoreEntry>();
        while (pending.TryDequeue(out var next))
        {
            foreach (var child in await scope.ChildrenAsync(next.Folder))
            {
                var childPath = next.Path.Length == 0 ? child.Placement.Placement.Name : next.Path + "/" + child.Placement.Placement.Name;
                if (child.Content.Content.Kind == MediaObjectKind.File)
                {
                    files.Add(Entry(childPath, child));
                    continue;
                }

                directories.Add(Entry(childPath, child));

                // A hard-linked folder is listed where it is placed, but never walked twice down
                // one path (the loop gate keeps the graph acyclic; the depth is a backstop).
                if (includeSubDirectories && next.Depth < 64)
                {
                    pending.Enqueue((childPath, child, next.Depth + 1));
                }
            }
        }

        // As the folder on disk listed them: directories, then files.
        foreach (var entry in directories.Concat(files))
        {
            yield return entry;
        }
    }

    public async Task<bool> TryCreateDirectoryAsync(string path)
    {
        if (await ScopeAsync() is not { } scope)
        {
            return await Legacy.TryCreateDirectoryAsync(path);
        }

        var existing = await scope.FindAsync(path);
        if (existing is { } found)
        {
            return found.Content.Content.Kind == MediaObjectKind.File
                ? throw new FileStoreException($"Cannot create directory because the path '{path}' already exists and is a file.")
                : false;
        }

        await scope.EnsureFolderAsync(path);
        return true;
    }

    public async Task<bool> TryDeleteFileAsync(string path)
    {
        if (await ScopeAsync() is not { } scope)
        {
            return await Legacy.TryDeleteFileAsync(path);
        }

        if (await scope.FindAsync(path) is not { Content.Content.Kind: MediaObjectKind.File } found)
        {
            return false;
        }

        await scope.WriteAsync(files => files.DeleteAsync(found.Placement.Placement.Id), path);
        return true;
    }

    public async Task<bool> TryDeleteDirectoryAsync(string path)
    {
        if (await ScopeAsync() is not { } scope)
        {
            return await Legacy.TryDeleteDirectoryAsync(path);
        }

        if (MediaLibraryPaths.Normalize(path).Length == 0)
        {
            throw new FileStoreException("Cannot delete the root directory.");
        }

        if (await scope.FindAsync(path) is not { Content.Content.Kind: MediaObjectKind.Folder } found)
        {
            return false;
        }

        await scope.WriteAsync(files => files.DeleteAsync(found.Placement.Placement.Id), path);
        return true;
    }

    public async Task MoveFileAsync(string oldPath, string newPath)
    {
        if (await ScopeAsync() is not { } scope)
        {
            await Legacy.MoveFileAsync(oldPath, newPath);
            return;
        }

        if (await scope.FindAsync(oldPath) is not { } source)
        {
            throw new FileStoreException($"Cannot move file '{oldPath}' because it does not exist.");
        }

        if (await scope.FindAsync(newPath) is not null)
        {
            throw new FileStoreException($"Cannot move file because the new path '{newPath}' already exists.");
        }

        var (parentPath, name) = MediaLibraryPaths.SplitLast(newPath);
        var parentId = await scope.EnsureFolderAsync(parentPath);
        var placementId = source.Placement.Placement.Id;
        await scope.WriteAsync(async files =>
        {
            if (source.Placement.Placement.ParentId != parentId)
            {
                await files.MoveAsync(placementId, parentId);
            }

            if (!string.Equals(source.Placement.Placement.Name, name, StringComparison.Ordinal))
            {
                await files.RenameAsync(placementId, name);
            }
        }, oldPath);
    }

    public async Task CopyFileAsync(string srcPath, string dstPath)
    {
        if (await ScopeAsync() is not { } scope)
        {
            await Legacy.CopyFileAsync(srcPath, dstPath);
            return;
        }

        if (await scope.FindAsync(srcPath) is not { Content.Content.Kind: MediaObjectKind.File } source)
        {
            throw new FileStoreException($"The file '{srcPath}' does not exist.");
        }

        if (await scope.FindAsync(dstPath) is not null)
        {
            throw new FileStoreException($"Cannot copy file because the destination path '{dstPath}' already exists.");
        }

        // A copy is a new file with its own id; its bytes are scanned like any upload.
        await using var bytes = await scope.OpenPublishedAsync(source) ?? throw new FileStoreException($"The file '{srcPath}' has no clean version to copy.");
        await CreateAsync(scope, dstPath, bytes, overwrite: false);
    }

    public async Task<Stream> GetFileStreamAsync(string path)
    {
        if (await ScopeAsync() is not { } scope)
        {
            return await Legacy.GetFileStreamAsync(path);
        }

        var found = await scope.FindAsync(path);
        return found is { Content.Content.Kind: MediaObjectKind.File } && await scope.OpenPublishedAsync(found.Value) is { } stream
            ? stream
            : throw new FileStoreException($"Cannot get file stream because the file '{path}' does not exist.");
    }

    public Task<Stream> GetFileStreamAsync(IFileStoreEntry fileStoreEntry) => GetFileStreamAsync(fileStoreEntry.Path);

    public async Task<string> CreateFileFromStreamAsync(string path, Stream inputStream, bool overwrite = false)
    {
        if (await ScopeAsync() is not { } scope)
        {
            return await Legacy.CreateFileFromStreamAsync(path, inputStream, overwrite);
        }

        return await CreateAsync(scope, path, inputStream, overwrite);
    }

    private static async Task<string> CreateAsync(LibraryScope scope, string path, Stream content, bool overwrite)
    {
        var normalized = MediaLibraryPaths.Normalize(path);
        var (parentPath, name) = MediaLibraryPaths.SplitLast(normalized);
        if (name.Length == 0)
        {
            throw new FileStoreException($"Cannot create file '{path}'.");
        }

        var claimedType = s_contentTypes.TryGetContentType(name, out var type) ? type : null;
        if (await scope.FindAsync(normalized) is { } existing)
        {
            if (existing.Content.Content.Kind != MediaObjectKind.File)
            {
                throw new FileStoreException($"Cannot create file '{path}' because it already exists as a directory.");
            }

            if (!overwrite)
            {
                throw new FileStoreException($"Cannot create file '{path}' because it already exists.");
            }

            // An overwrite replaces what is served: the new version is published once it is clean.
            await scope.WriteAsync(files => files.ReplaceAsync(existing.Placement.Placement.Id, content, claimedType, publishWhenClean: true), path);
            return normalized;
        }

        var parentId = await scope.EnsureFolderAsync(parentPath);
        await scope.WriteAsync(files => files.UploadAsync(new MediaUpload(parentId, name, content, claimedType)), path);
        return normalized;
    }

    private static LibraryFileStoreEntry Entry(string path, Found found)
    {
        var content = found.Content.Content;
        var version = content.Versions.FirstOrDefault(candidate => candidate.Number == content.PublishedVersion) ?? content.Versions.LastOrDefault();
        var (directory, _) = MediaLibraryPaths.SplitLast(path);
        return new LibraryFileStoreEntry(
            path,
            found.Placement.Placement.Name,
            directory,
            version?.Size ?? 0,
            content.ModifiedUtc.UtcDateTime,
            content.Kind != MediaObjectKind.File);
    }

    /// <summary>The library's scoped services, or null while the library is not active (or there
    /// is no shell scope), when the folder on disk serves.</summary>
    private static async Task<LibraryScope?> ScopeAsync()
    {
        var services = ShellScope.Services;
        var library = services?.GetService<IMediaLibrary>();
        if (services is null || library is null || !await library.IsActiveAsync())
        {
            return null;
        }

        return new LibraryScope(services, library, services.GetRequiredService<IMediaObjectStore>(), services.GetRequiredService<IMediaStorageProvider>());
    }

    private readonly record struct Found(MediaPlacementDocument Placement, MediaContentDocument Content);

    private sealed class LibraryScope(IServiceProvider services, IMediaLibrary library, IMediaObjectStore store, IMediaStorageProvider storage)
    {
        public async Task<Found?> FindAsync(string? path)
        {
            if (MediaLibraryPaths.Split(path) is null || await library.ResolvePathAsync(path ?? string.Empty) is not { } placementId)
            {
                return null;
            }

            return await LoadAsync(placementId);
        }

        public async Task<IReadOnlyList<Found>> ChildrenAsync(Found folder)
        {
            var target = await TargetAsync(folder);
            if (target is null)
            {
                return [];
            }

            var holders = await store.GetPlacementsOfContentAsync(target.Value.Content.Content.Id);
            var children = await store.GetChildrenAsync([.. holders.Select(holder => holder.Placement.Id)]);
            var contents = (await store.GetContentsAsync([.. children.Select(child => child.Placement.ContentId)]))
                .ToDictionary(content => content.Content.Id, StringComparer.Ordinal);

            return [.. children
                .Where(child => contents.ContainsKey(child.Placement.ContentId))
                .Select(child => new Found(child, contents[child.Placement.ContentId]))
                .OrderBy(child => child.Placement.Placement.Name, StringComparer.OrdinalIgnoreCase)];
        }

        /// <summary>The published, clean bytes, unchecked: the surface that asked decided by path.</summary>
        public async Task<Stream?> OpenPublishedAsync(Found found)
        {
            var target = await TargetAsync(found);
            var content = target?.Content.Content;
            var version = content?.Versions.FirstOrDefault(candidate => candidate.Number == content.PublishedVersion);
            return version is { State: MediaVersionState.Clean }
                ? await storage.OpenReadAsync(MediaStorageArea.Committed, version.BlobKey)
                : null;
        }

        /// <summary>The placement of the folder at <paramref name="path"/>, creating what is missing.</summary>
        public async Task<string> EnsureFolderAsync(string path)
        {
            var segments = MediaLibraryPaths.Split(path) ?? throw new FileStoreException($"The path '{path}' is not valid.");
            var current = await library.GetDriveIdAsync() ?? throw new FileStoreException("The media library has no drive.");
            var walked = string.Empty;
            foreach (var segment in segments)
            {
                walked = walked.Length == 0 ? segment : walked + "/" + segment;
                if (await FindAsync(walked) is { } found)
                {
                    if (found.Content.Content.Kind == MediaObjectKind.File)
                    {
                        throw new FileStoreException($"Cannot create directory because the path '{walked}' already exists and is a file.");
                    }

                    current = found.Placement.Placement.Id;
                    continue;
                }

                var parentId = current;
                var created = await WriteAsync(files => files.CreateFolderAsync(parentId, segment), walked);
                current = created.Placement.Id;
            }

            return current;
        }

        public Task WriteAsync(Func<IMediaObjectWrites, Task> work, string path) =>
            WriteAsync(async files =>
            {
                await work(files);
                return true;
            }, path);

        public async Task<T> WriteAsync<T>(Func<IMediaObjectWrites, Task<T>> work, string path)
        {
            var files = services.GetRequiredService<IMediaObjectWrites>();
            try
            {
                return await MediaCallers.AsCallerOrSystemAsync(services, () => work(files));
            }
            catch (Exception exception) when (exception is MediaObjectNotFoundException or MediaObjectConflictException or ArgumentException)
            {
                throw new FileStoreException($"Cannot change '{path}': {exception.Message}", exception);
            }
        }

        private async Task<Found?> TargetAsync(Found found) =>
            found.Placement.Placement.LinkTargetPlacementId is { } target ? await LoadAsync(target) : found;

        private async Task<Found?> LoadAsync(string placementId)
        {
            var placement = await store.GetPlacementAsync(placementId);
            var content = placement is null ? null : await store.GetContentAsync(placement.Placement.ContentId);
            return placement is null || content is null ? null : new Found(placement, content);
        }
    }
}

public sealed class LibraryFileStoreEntry(string path, string name, string directoryPath, long length, DateTime lastModifiedUtc, bool isDirectory) : IFileStoreEntry
{
    public string Path { get; } = path;

    public string Name { get; } = name;

    public string DirectoryPath { get; } = directoryPath;

    public long Length { get; } = length;

    public DateTime LastModifiedUtc { get; } = lastModifiedUtc;

    public bool IsDirectory { get; } = isDirectory;
}
