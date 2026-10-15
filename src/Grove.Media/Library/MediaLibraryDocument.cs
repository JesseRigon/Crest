#nullable enable
using Grove.Data.Documents;
using Grove.Documents;

namespace Grove.Media.Library;

/// <summary>
/// The tenant's media library (ruling F1): which shared drive the <c>/media</c> tree lives in,
/// and how far its one-time import got. <c>/media</c> is served from the drive once
/// <see cref="ImportedUtc"/> is set; until then the media folder on disk serves it as before.
/// </summary>
public sealed class MediaLibraryDocument : Document
{
    /// <summary>The library drive's placement id.</summary>
    public string? DrivePlacementId { get; set; }

    public DateTime? ImportStartedUtc { get; set; }

    /// <summary>When the tree was imported and the drive took over <c>/media</c>.</summary>
    public DateTime? ImportedUtc { get; set; }

    /// <summary>When media fields were last rewritten from paths to placement ids.</summary>
    public DateTime? FieldsRewrittenUtc { get; set; }

    /// <summary>How the import entered existing files: scanned, or trusted as clean.</summary>
    public bool TrustedExisting { get; set; }

    public MediaLibraryImportReport? LastReport { get; set; }
}

/// <summary>What one import run did.</summary>
public sealed class MediaLibraryImportReport
{
    public int FoldersCreated { get; set; }

    public int FilesImported { get; set; }

    /// <summary>Already in the drive (an earlier run, or uploaded since): left as they are.</summary>
    public int Skipped { get; set; }

    /// <summary>Sharing entries written: secure media folder rights and path entries moved to placements.</summary>
    public int SharingEntries { get; set; }

    public int ContentItemsRewritten { get; set; }

    /// <summary>Paths that could not be imported, with the reason.</summary>
    public List<string> Failures { get; set; } = [];
}

/// <summary>Where <see cref="MediaLibraryDocument"/> is kept; a seam so the library can be tested
/// without a document store.</summary>
public interface IMediaLibraryState
{
    /// <summary>The cached, read-only document.</summary>
    Task<MediaLibraryDocument> GetAsync();

    /// <summary>A copy to change and save.</summary>
    Task<MediaLibraryDocument> LoadForUpdateAsync();

    Task SaveAsync(MediaLibraryDocument document);
}

public sealed class DocumentMediaLibraryState(IDocumentManager<MediaLibraryDocument> documents) : IMediaLibraryState
{
    public Task<MediaLibraryDocument> GetAsync() => documents.GetOrCreateImmutableAsync();

    public Task<MediaLibraryDocument> LoadForUpdateAsync() => documents.GetOrCreateMutableAsync();

    public Task SaveAsync(MediaLibraryDocument document) => documents.UpdateAsync(document);
}
