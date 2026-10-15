namespace Grove.Media.Files;

public enum ScanOutcome
{
    Clean,
    Rejected,
    Failed,

    /// <summary>An asynchronous provider accepted the job and reports its verdict later.</summary>
    Pending,
}

public sealed record ScanVerdict(ScanOutcome Outcome, string? Reason = null, string? DetectedType = null)
{
    public static readonly ScanVerdict Clean = new(ScanOutcome.Clean);

    public static ScanVerdict Rejected(string reason) => new(ScanOutcome.Rejected, reason);

    public static ScanVerdict Failed(string reason) => new(ScanOutcome.Failed, reason);

    public static readonly ScanVerdict Pending = new(ScanOutcome.Pending);
}

/// <summary>A quarantined upload handed to a scanner: its bytes, or a time-limited reference to them.</summary>
public sealed record ScanRequest(string ScanId, string Tenant, string Name, string? ClaimedType, long Size, Func<CancellationToken, Task<Stream>> OpenAsync, Uri? TemporaryUri);

/// <summary>
/// A host-level scanner (docs/media.md § 2, requirements 86-95): malware and content checks,
/// one interface for local and offsite providers. A failure keeps the upload in quarantine; no
/// verdict ever comes from the client.
/// </summary>
public interface IUploadScanner
{
    string Name { get; }

    Task<ScanVerdict> ScanAsync(ScanRequest request, CancellationToken cancellationToken = default);
}

/// <summary>Identifies a file's real type from its content, as a MIME type (requirement 91).</summary>
public interface IFileTypeDetector
{
    string Name { get; }

    Task<string?> DetectAsync(Stream content, CancellationToken cancellationToken = default);
}

/// <summary>
/// The quarantine flow: every upload, resumable ones included, lands here; scanners and type
/// detection run on the server side; an asynchronous provider reports back through
/// <see cref="ReportVerdictAsync"/>; a clean verdict commits the version, anything else keeps
/// or rejects it.
/// </summary>
public interface IUploadQuarantine
{
    /// <summary>Holds the bytes in quarantine and starts scanning; returns the scan id.</summary>
    Task<string> SubmitAsync(string contentId, int version, string name, string? claimedType, Stream content, CancellationToken cancellationToken = default);

    Task ReportVerdictAsync(string scanId, string scannerName, ScanVerdict verdict, CancellationToken cancellationToken = default);
}

/// <summary>
/// Where the quarantine reports a final verdict for a version: the file-object store commits a
/// clean version (and publishes it when it is the first), records a rejection with its reason,
/// or keeps a failed one in quarantine.
/// </summary>
public interface IMediaVerdictHandler
{
    Task OnVerdictAsync(string contentId, int version, ScanVerdict verdict, CancellationToken cancellationToken = default);
}
