namespace Crest.Media.Services;

public sealed class TusUploadEntry
{
    /// <summary>
    /// The destination path within the media store (e.g. "images/photos").
    /// </summary>
    public string DestinationPath { get; set; }

    /// <summary>
    /// The normalized file name.
    /// </summary>
    public string FileName { get; set; }

    /// <summary>
    /// The final media file path after the upload is completed and stored.
    /// Set by <c>OnFileCompleteAsync</c>.
    /// </summary>
    public string MediaFilePath { get; set; }

    /// <summary>
    /// The folder or drive placement a file-object upload lands in (docs/media.md § 3); null
    /// for an upload into the media library by path.
    /// </summary>
    public string ParentPlacementId { get; set; }

    /// <summary>The file object's placement once the upload completed. Set by <c>OnFileCompleteAsync</c>.</summary>
    public string PlacementId { get; set; }
}
