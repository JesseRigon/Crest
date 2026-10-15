#nullable enable
using Grove.Media.Files;

namespace Grove.Media.Library;

/// <summary>
/// An upload addressed by media library path (the Tus <c>destinationPath</c>, ruling F1): the
/// folder path is resolved to its placement in the library drive and the bytes go through
/// <see cref="IMediaObjectService.UploadAsync"/>, so they land in quarantine like every upload
/// and are served once clean. Nothing is written to the media folder on disk.
/// </summary>
public static class MediaLibraryUploads
{
    /// <summary>
    /// Uploads <paramref name="content"/> as <paramref name="filePath"/> (folder and name).
    /// </summary>
    /// <param name="createFolder">Creates a missing folder chain by path, as the caller (the
    /// media store's <c>TryCreateDirectoryAsync</c>, which checks Editor on the parent).</param>
    /// <returns>The new placement; null while the library is not active, when the caller keeps
    /// the media folder's own path.</returns>
    /// <exception cref="MediaObjectNotFoundException">The folder does not exist for the caller.</exception>
    public static async Task<MediaItem?> UploadAsync(
        IMediaLibrary library,
        IMediaObjectService files,
        Func<string, Task<bool>> createFolder,
        string filePath,
        Stream content,
        string? claimedType,
        CancellationToken cancellationToken = default)
    {
        if (!await library.IsActiveAsync(cancellationToken))
        {
            return null;
        }

        var (folderPath, name) = MediaLibraryPaths.SplitLast(filePath);
        var folderId = await library.ResolvePathAsync(folderPath, cancellationToken);
        if (folderId is null && await createFolder(folderPath))
        {
            folderId = await library.ResolvePathAsync(folderPath, cancellationToken);
        }

        if (folderId is null)
        {
            throw new MediaObjectNotFoundException();
        }

        return await files.UploadAsync(new MediaUpload(folderId, name, content, claimedType), cancellationToken);
    }
}
