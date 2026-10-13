using Crest.FileStorage.AzureBlob;

namespace Crest.Media.Azure;

/// <summary>
/// Where file objects' bytes live in Azure Blob Storage (docs/media.md § 3, requirements 10-11):
/// the section <c>Crest_Media_Azure_Files</c> (connection string, container, base path, with the
/// same Liquid templating as the media options). When it is not configured the media storage's
/// account and container are used, under the media base path followed by <c>-files</c>
/// (<c>crest-files</c> when the media base path is empty), so the media library's own tree never
/// lists them.
/// </summary>
public sealed class MediaBlobFilesStorageOptions : BlobStorageOptions
{
    public const string DefaultBasePath = "crest-files";

    /// <summary>The options to use: the configured ones, else derived from the media options.</summary>
    public static BlobStorageOptions Resolve(MediaBlobFilesStorageOptions configured, MediaBlobStorageOptions media)
    {
        if (configured.IsConfigured())
        {
            return configured;
        }

        var basePath = media.BasePath?.Trim('/');
        return new MediaBlobFilesStorageOptions
        {
            ConnectionString = media.ConnectionString,
            ContainerName = media.ContainerName,
            BasePath = string.IsNullOrEmpty(basePath) ? DefaultBasePath : basePath + "-files",
            UseHierarchicalNamespace = media.UseHierarchicalNamespace,
        };
    }
}
