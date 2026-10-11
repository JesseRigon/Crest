using Crest.Media.Files;

namespace Crest.Media.Scanning.Services;

/// <summary>
/// Combines the scanners' verdicts and the type check into one (requirement 92): any rejection
/// rejects; otherwise any failure fails, which keeps the upload quarantined; otherwise anything
/// still running is pending; only when every scanner is clean and the type matches is it clean.
/// </summary>
public static class VerdictAggregator
{
    public const string NoScannersReason = "No upload scanner is configured.";

    /// <param name="scanners">One verdict per planned scanner; null for a scanner that has not run.</param>
    /// <param name="typeCheck">The type check (<see cref="TypeConsistency"/>); pending while detection has not run.</param>
    public static ScanVerdict Aggregate(IReadOnlyCollection<ScanVerdict?> scanners, ScanVerdict typeCheck, bool allowNoScanners = false)
    {
        ArgumentNullException.ThrowIfNull(scanners);
        ArgumentNullException.ThrowIfNull(typeCheck);

        var detectedType = typeCheck.DetectedType;

        if (typeCheck.Outcome == ScanOutcome.Rejected)
        {
            return typeCheck;
        }

        if (scanners.FirstOrDefault(verdict => verdict?.Outcome == ScanOutcome.Rejected) is { } rejected)
        {
            return new ScanVerdict(ScanOutcome.Rejected, rejected.Reason, detectedType);
        }

        if (scanners.Count == 0 && !allowNoScanners)
        {
            return new ScanVerdict(ScanOutcome.Failed, NoScannersReason, detectedType);
        }

        var failures = scanners
            .Where(verdict => verdict?.Outcome == ScanOutcome.Failed)
            .Select(verdict => verdict!.Reason)
            .Prepend(typeCheck.Outcome == ScanOutcome.Failed ? typeCheck.Reason : null)
            .Where(reason => !string.IsNullOrEmpty(reason))
            .ToList();

        if (typeCheck.Outcome == ScanOutcome.Failed || scanners.Any(verdict => verdict?.Outcome == ScanOutcome.Failed))
        {
            return new ScanVerdict(ScanOutcome.Failed, failures.Count > 0 ? string.Join(" ", failures) : "Scanning failed.", detectedType);
        }

        if (typeCheck.Outcome == ScanOutcome.Pending || scanners.Any(verdict => verdict is null || verdict.Outcome == ScanOutcome.Pending))
        {
            return new ScanVerdict(ScanOutcome.Pending, null, detectedType);
        }

        return new ScanVerdict(ScanOutcome.Clean, null, detectedType);
    }
}
