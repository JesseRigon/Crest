namespace Crest.Media.Files;

/// <summary>
/// The storage key of a version's bytes, shared by the quarantine and the file-object store so
/// both address the same blob: <see cref="IUploadQuarantine.SubmitAsync"/> takes the content id
/// and version, never a key. Opaque to users: never a path they see (requirement 10).
/// </summary>
public static class MediaBlobKeys
{
    public static string ForVersion(string contentId, int version)
    {
        ArgumentException.ThrowIfNullOrEmpty(contentId);
        ArgumentOutOfRangeException.ThrowIfNegative(version);

        return $"{contentId}/v{version}";
    }
}
