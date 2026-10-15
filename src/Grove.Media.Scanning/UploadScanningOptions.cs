namespace Grove.Media.Scanning;

/// <summary>
/// Host-level upload scanning (docs/media.md § 2, requirements 86, 90, 91): which scanners run for
/// every tenant, which type detector identifies files, and how failures are retried. Bound from
/// the <c>Grove_Media_Scanning</c> configuration section, which the host operator controls.
/// </summary>
public sealed class UploadScanningOptions
{
    public const string ConfigSection = "Grove_Media_Scanning";

    /// <summary>The host's scanners, by name: a registered provider (<c>ClamAV</c>) or an entry of <see cref="Offsite"/>.</summary>
    public List<string> Scanners { get; set; } = [];

    /// <summary>Offsite scanners reachable through an API (requirement 94), by name.</summary>
    public Dictionary<string, OffsiteScannerOptions> Offsite { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary><c>libmagic</c> or <c>signature</c> (the built-in fallback, used when unset).</summary>
    public string? TypeDetector { get; set; }

    /// <summary>The libmagic database; the one shipped with the Mime package when unset.</summary>
    public string? LibmagicDatabasePath { get; set; }

    /// <summary>How much of a file libmagic reads.</summary>
    public int LibmagicReadBytes { get; set; } = 1024 * 1024;

    /// <summary>
    /// Development only: with no scanner configured, uploads pass on type detection alone. Off by
    /// default, so a host without scanners keeps every upload in quarantine (requirement 86).
    /// </summary>
    public bool AllowNoScanners { get; set; }

    /// <summary>Strips location (EXIF GPS, XMP) from JPEG images before commit (requirement 87).</summary>
    public bool StripImageLocation { get; set; } = true;

    public long StripImageLocationMaxBytes { get; set; } = 64L * 1024 * 1024;

    /// <summary>Deletes a rejected upload's bytes from quarantine; the scan job keeps the record and reason.</summary>
    public bool DeleteRejected { get; set; } = true;

    public TimeSpan RetryDelay { get; set; } = TimeSpan.FromMinutes(1);

    public TimeSpan MaxRetryDelay { get; set; } = TimeSpan.FromHours(1);

    /// <summary>How long an offsite scanner may stay pending before it is asked again.</summary>
    public TimeSpan PendingTimeout { get; set; } = TimeSpan.FromHours(1);

    /// <summary>The lifetime of the reference an offsite scanner fetches the quarantined file by.</summary>
    public TimeSpan TemporaryUriLifetime { get; set; } = TimeSpan.FromMinutes(30);

    /// <summary>How far a callback's timestamp may be from now.</summary>
    public TimeSpan CallbackClockSkew { get; set; } = TimeSpan.FromMinutes(5);

    /// <summary>The public base URL offsite scanners call back on; the tenant's request host when unset.</summary>
    public string? CallbackBaseUrl { get; set; }
}

/// <summary>An offsite scanner: where jobs are posted, and the shared secret both directions are signed with.</summary>
public sealed class OffsiteScannerOptions
{
    public string? Url { get; set; }

    public string? Secret { get; set; }
}
