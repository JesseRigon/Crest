using Grove.Data.Storage;

namespace Grove.Media;

/// <summary>
/// Cache a media file store.
/// </summary>
public interface IMediaFileStoreCache : IFileStoreCache
{
    Task<bool> PurgeAsync();
    Task<bool> TryDeleteFileAsync(string path);
    Task<bool> TryDeleteDirectoryAsync(string path);
}
