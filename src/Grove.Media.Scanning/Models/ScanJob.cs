using Grove.Media.Files;

namespace Grove.Media.Scanning.Models;

/// <summary>
/// One quarantined upload being scanned: a document in its own tenant's store, so uploads,
/// quarantine and results are visible only to that tenant (requirement 89).
/// </summary>
public sealed class ScanJob
{
    public string ScanId { get; set; } = string.Empty;

    public string Tenant { get; set; } = string.Empty;

    public string ContentId { get; set; } = string.Empty;

    public int Version { get; set; }

    public string Name { get; set; } = string.Empty;

    public string? ClaimedType { get; set; }

    public long Size { get; set; }

    public string StorageKey { get; set; } = string.Empty;

    /// <summary>Pending while scanning; Clean and Rejected are final; Failed stays quarantined and is retried.</summary>
    public ScanOutcome Status { get; set; } = ScanOutcome.Pending;

    public string? Reason { get; set; }

    public string? Detector { get; set; }

    public bool TypeDetected { get; set; }

    public string? DetectedType { get; set; }

    public string? DetectorError { get; set; }

    /// <summary>The scanners chosen at submission (host and tenant), each with its own state.</summary>
    public List<ScannerState> Scanners { get; set; } = [];

    public int Attempts { get; set; }

    public bool LocationStripped { get; set; }

    public bool Committed { get; set; }

    /// <summary>The last outcome the verdict handler accepted; null until one did.</summary>
    public ScanOutcome? NotifiedOutcome { get; set; }

    public DateTime CreatedUtc { get; set; }

    public DateTime? CompletedUtc { get; set; }

    /// <summary>When the background task should look at it again; null when nothing is left to do.</summary>
    public DateTime? NextAttemptUtc { get; set; }

    public bool IsFinal => Status is ScanOutcome.Clean or ScanOutcome.Rejected;

    public ScannerState? GetScanner(string name)
        => Scanners.FirstOrDefault(scanner => string.Equals(scanner.Name, name, StringComparison.OrdinalIgnoreCase));
}

public sealed class ScannerState
{
    public string Name { get; set; } = string.Empty;

    /// <summary>Null until the scanner has run.</summary>
    public ScanOutcome? Outcome { get; set; }

    public string? Reason { get; set; }

    public string? DetectedType { get; set; }

    public DateTime? UpdatedUtc { get; set; }

    public ScanVerdict? ToVerdict()
        => Outcome is { } outcome ? new ScanVerdict(outcome, Reason, DetectedType) : null;

    public void Apply(ScanVerdict verdict, DateTime now)
    {
        Outcome = verdict.Outcome;
        Reason = verdict.Reason;
        DetectedType = verdict.DetectedType;
        UpdatedUtc = now;
    }
}
