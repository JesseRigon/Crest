namespace Crest.Media.Scanning;

/// <summary>
/// A tenant's choice (site settings, requirement 90): its own external scanner, and whether the
/// host's scanners still run alongside it. Without an external scanner the host's always run.
/// Type detection stays the host's.
/// </summary>
public sealed class UploadScanningSettings
{
    /// <summary>Where the tenant's external scanner accepts jobs; none when empty.</summary>
    public string? ExternalScannerUrl { get; set; }

    /// <summary>The secret the tenant's scanner signs its verdicts with, and Crest its jobs.</summary>
    // TODO: protect at rest with data protection once the settings editor exists.
    public string? ExternalScannerSecret { get; set; }

    /// <summary>Only the tenant's scanner runs; ignored without one.</summary>
    public bool BypassHostScanners { get; set; }

    public bool HasExternalScanner => !string.IsNullOrWhiteSpace(ExternalScannerUrl);
}
