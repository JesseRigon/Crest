using Grove.Locking.Distributed;
using Grove.Media.Files;
using Grove.Media.Scanning.Models;
using Grove.Media.Scanning.Processing;
using Grove.Modules;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Grove.Media.Scanning.Services;

/// <summary>
/// Runs a quarantined upload through type detection and its scanners, on the server only
/// (requirement 93), records each verdict, and acts on the aggregate: clean commits the bytes,
/// rejected keeps only the record, failed stays in quarantine and is retried with backoff.
/// </summary>
public sealed class ScanCoordinator
{
    private static readonly TimeSpan s_lockTimeout = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan s_lockExpiration = TimeSpan.FromMinutes(30);

    private readonly IScanJobStore _store;
    private readonly ScannerCatalog _catalog;
    private readonly IFileTypeDetector _detector;
    private readonly IMediaStorageProvider _storage;
    private readonly IServiceProvider _serviceProvider;
    private readonly UploadScanningOptions _options;
    private readonly IClock _clock;
    private readonly ILogger _logger;

    public ScanCoordinator(
        IScanJobStore store,
        ScannerCatalog catalog,
        IFileTypeDetector detector,
        IMediaStorageProvider storage,
        IServiceProvider serviceProvider,
        IOptions<UploadScanningOptions> options,
        IClock clock,
        ILogger<ScanCoordinator> logger)
    {
        _store = store;
        _catalog = catalog;
        _detector = detector;
        _storage = storage;
        _serviceProvider = serviceProvider;
        _options = options.Value;
        _clock = clock;
        _logger = logger;
    }

    /// <summary>Detects, scans whatever has not run (or failed, or timed out pending), and evaluates.</summary>
    public async Task RunAsync(string scanId, CancellationToken cancellationToken = default)
    {
        await using var locker = await LockAsync(scanId);
        if (locker is null)
        {
            return;
        }

        var job = await _store.GetAsync(scanId, cancellationToken);
        if (job is null)
        {
            _logger.LogWarning("Scan job '{ScanId}' was not found.", scanId);
            return;
        }

        if (job.IsFinal)
        {
            // A final verdict the handler has not accepted yet is delivered again.
            await EvaluateAsync(job, cancellationToken);
            return;
        }

        job.Attempts++;

        if (!job.TypeDetected)
        {
            await DetectTypeAsync(job, cancellationToken);
        }

        if (TypeCheck(job).Outcome != ScanOutcome.Rejected)
        {
            await RunScannersAsync(job, cancellationToken);
        }

        await EvaluateAsync(job, cancellationToken);
    }

    /// <summary>An asynchronous scanner's verdict, reported back (requirement 92).</summary>
    public async Task<bool> ReportAsync(string scanId, string scannerName, ScanVerdict verdict, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(verdict);

        if (verdict.Outcome == ScanOutcome.Pending)
        {
            return true;
        }

        await using var locker = await LockAsync(scanId);
        if (locker is null)
        {
            return false;
        }

        var job = await _store.GetAsync(scanId, cancellationToken);
        if (job is null)
        {
            return false;
        }

        var state = job.GetScanner(scannerName);
        if (state is null)
        {
            _logger.LogWarning("Scanner '{Scanner}' reported on scan '{ScanId}' it was not asked to scan.", scannerName, scanId);
            return false;
        }

        if (job.IsFinal)
        {
            return true;
        }

        state.Apply(verdict, _clock.UtcNow);
        AdoptDetectedType(job, verdict);

        await EvaluateAsync(job, cancellationToken);

        return true;
    }

    internal static ScanVerdict TypeCheck(ScanJob job)
        => TypeConsistency.Check(job.Name, job.ClaimedType, job.TypeDetected, job.DetectedType, job.DetectorError);

    internal static ScanVerdict Aggregate(ScanJob job, bool allowNoScanners)
        => VerdictAggregator.Aggregate([.. job.Scanners.Select(scanner => scanner.ToVerdict())], TypeCheck(job), allowNoScanners);

    private async Task DetectTypeAsync(ScanJob job, CancellationToken cancellationToken)
    {
        try
        {
            await using var stream = await OpenQuarantinedAsync(job, cancellationToken);
            job.DetectedType = await _detector.DetectAsync(stream, cancellationToken);
            job.Detector = _detector.Name;
            job.TypeDetected = true;
            job.DetectorError = null;
        }
        catch (Exception exception) when (exception is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
        {
            _logger.LogError(exception, "Type detection with '{Detector}' failed for scan '{ScanId}'.", _detector.Name, job.ScanId);
            job.DetectorError = exception.Message;
        }
    }

    private async Task RunScannersAsync(ScanJob job, CancellationToken cancellationToken)
    {
        var now = _clock.UtcNow;
        Uri? temporaryUri = null;
        var temporaryUriRequested = false;

        foreach (var state in job.Scanners)
        {
            var due = state.Outcome switch
            {
                null or ScanOutcome.Failed => true,
                ScanOutcome.Pending => state.UpdatedUtc is null || now - state.UpdatedUtc.Value >= _options.PendingTimeout,
                _ => false,
            };

            if (!due)
            {
                continue;
            }

            var scanner = await _catalog.FindAsync(state.Name);
            if (scanner is null)
            {
                state.Apply(ScanVerdict.Failed($"The scanner '{state.Name}' is not available."), now);
                continue;
            }

            if (!temporaryUriRequested)
            {
                temporaryUriRequested = true;
                temporaryUri = await TryGetTemporaryUriAsync(job, cancellationToken);
            }

            var request = new ScanRequest(job.ScanId, job.Tenant, job.Name, job.ClaimedType, job.Size, token => OpenQuarantinedAsync(job, token), temporaryUri);

            ScanVerdict verdict;
            try
            {
                verdict = await scanner.ScanAsync(request, cancellationToken);
            }
            catch (Exception exception) when (exception is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
            {
                _logger.LogError(exception, "Scanner '{Scanner}' failed on scan '{ScanId}'.", state.Name, job.ScanId);
                verdict = ScanVerdict.Failed($"The scanner '{state.Name}' failed: {exception.Message}");
            }

            state.Apply(verdict, _clock.UtcNow);
            AdoptDetectedType(job, verdict);

            if (verdict.Outcome == ScanOutcome.Rejected)
            {
                break;
            }
        }
    }

    private async Task EvaluateAsync(ScanJob job, CancellationToken cancellationToken)
    {
        var now = _clock.UtcNow;
        var verdict = job.IsFinal
            ? new ScanVerdict(job.Status, job.Reason, job.DetectedType)
            : Aggregate(job, _options.AllowNoScanners);

        switch (verdict.Outcome)
        {
            case ScanOutcome.Pending:
                job.Status = ScanOutcome.Pending;
                job.Reason = null;
                job.NextAttemptUtc = NextPendingCheck(job, now);
                break;

            case ScanOutcome.Failed:
                job.Status = ScanOutcome.Failed;
                job.Reason = verdict.Reason;
                job.NextAttemptUtc = now + Backoff(job.Attempts);
                break;

            case ScanOutcome.Rejected:
                job.Status = ScanOutcome.Rejected;
                job.Reason = verdict.Reason;
                job.CompletedUtc ??= now;
                await DeleteRejectedAsync(job, cancellationToken);
                break;

            case ScanOutcome.Clean:
                if (!await CommitAsync(job, cancellationToken))
                {
                    verdict = new ScanVerdict(ScanOutcome.Failed, job.Reason, job.DetectedType);
                    break;
                }

                job.Status = ScanOutcome.Clean;
                job.Reason = null;
                job.CompletedUtc ??= now;
                break;
        }

        if (verdict.Outcome != ScanOutcome.Pending && job.NotifiedOutcome != verdict.Outcome)
        {
            if (await NotifyAsync(job, verdict, cancellationToken))
            {
                job.NotifiedOutcome = verdict.Outcome;
            }
        }

        if (job.IsFinal)
        {
            job.NextAttemptUtc = job.NotifiedOutcome == job.Status ? null : now + Backoff(job.Attempts);
        }

        await _store.SaveAsync(job, commit: true, cancellationToken);
    }

    private async Task<bool> CommitAsync(ScanJob job, CancellationToken cancellationToken)
    {
        if (job.Committed)
        {
            return true;
        }

        try
        {
            if (_options.StripImageLocation && !job.LocationStripped)
            {
                await StripLocationAsync(job, cancellationToken);
                job.LocationStripped = true;
            }

            await _storage.CommitAsync(job.StorageKey, cancellationToken);
            job.Committed = true;

            return true;
        }
        catch (Exception exception) when (exception is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
        {
            _logger.LogError(exception, "Committing clean scan '{ScanId}' failed; it stays in quarantine.", job.ScanId);
            job.Status = ScanOutcome.Failed;
            job.Reason = $"Committing the clean upload failed: {exception.Message}";
            job.NextAttemptUtc = _clock.UtcNow + Backoff(job.Attempts);

            return false;
        }
    }

    private async Task StripLocationAsync(ScanJob job, CancellationToken cancellationToken)
    {
        var type = TypeConsistency.Normalize(job.DetectedType);
        if (!ImageLocationStripper.Handles(type))
        {
            return;
        }

        if (job.Size > _options.StripImageLocationMaxBytes)
        {
            _logger.LogWarning("Scan '{ScanId}' is too large to strip its location data ({Size} bytes).", job.ScanId, job.Size);
            return;
        }

        byte[] original;
        await using (var stream = await OpenQuarantinedAsync(job, cancellationToken))
        using (var buffer = new MemoryStream())
        {
            await stream.CopyToAsync(buffer, cancellationToken);
            original = buffer.ToArray();
        }

        if (ImageLocationStripper.TryStrip(type, original, out var stripped))
        {
            using var content = new MemoryStream(stripped, writable: false);
            await _storage.WriteAsync(MediaStorageArea.Quarantine, job.StorageKey, content, cancellationToken);
            job.Size = stripped.Length;
        }
    }

    private async Task DeleteRejectedAsync(ScanJob job, CancellationToken cancellationToken)
    {
        if (!_options.DeleteRejected)
        {
            return;
        }

        try
        {
            await _storage.DeleteAsync(MediaStorageArea.Quarantine, job.StorageKey, cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
        {
            _logger.LogError(exception, "Deleting rejected scan '{ScanId}' from quarantine failed.", job.ScanId);
        }
    }

    private async Task<bool> NotifyAsync(ScanJob job, ScanVerdict verdict, CancellationToken cancellationToken)
    {
        var handler = _serviceProvider.GetService<IMediaVerdictHandler>();
        if (handler is null)
        {
            _logger.LogWarning(
                "No media verdict handler is registered; the {Outcome} verdict for '{ContentId}' version {Version} is recorded on scan '{ScanId}' only.",
                verdict.Outcome, job.ContentId, job.Version, job.ScanId);

            return false;
        }

        try
        {
            await handler.OnVerdictAsync(job.ContentId, job.Version, verdict, cancellationToken);
            return true;
        }
        catch (Exception exception) when (exception is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
        {
            _logger.LogError(exception, "The media verdict handler failed for scan '{ScanId}'; it is delivered again later.", job.ScanId);
            return false;
        }
    }

    private async Task<Stream> OpenQuarantinedAsync(ScanJob job, CancellationToken cancellationToken)
        => await _storage.OpenReadAsync(MediaStorageArea.Quarantine, job.StorageKey, cancellationToken)
            ?? throw new FileNotFoundException($"The quarantined upload '{job.StorageKey}' is missing.");

    private async Task<Uri?> TryGetTemporaryUriAsync(ScanJob job, CancellationToken cancellationToken)
    {
        try
        {
            return await _storage.GetTemporaryReadUriAsync(MediaStorageArea.Quarantine, job.StorageKey, _options.TemporaryUriLifetime, cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
        {
            _logger.LogWarning(exception, "No temporary reference for scan '{ScanId}'; offsite scanners get the bytes streamed.", job.ScanId);
            return null;
        }
    }

    private static void AdoptDetectedType(ScanJob job, ScanVerdict verdict)
    {
        // A scanner that also detects types can supply the type when the detector could not (requirement 91).
        if (!string.IsNullOrEmpty(verdict.DetectedType) && (!job.TypeDetected || job.DetectedType is null || job.DetectedType == "application/octet-stream"))
        {
            job.DetectedType = verdict.DetectedType;
            job.TypeDetected = true;
            job.DetectorError = null;
        }
    }

    private DateTime NextPendingCheck(ScanJob job, DateTime now)
    {
        var pendingSince = job.Scanners
            .Where(scanner => scanner.Outcome == ScanOutcome.Pending && scanner.UpdatedUtc is not null)
            .Select(scanner => scanner.UpdatedUtc!.Value)
            .DefaultIfEmpty(now)
            .Min();

        var check = pendingSince + _options.PendingTimeout;

        return check > now ? check : now;
    }

    internal TimeSpan Backoff(int attempts)
    {
        var exponent = Math.Clamp(attempts - 1, 0, 20);
        var delay = TimeSpan.FromTicks(_options.RetryDelay.Ticks * (1L << exponent));

        return delay > _options.MaxRetryDelay || delay <= TimeSpan.Zero ? _options.MaxRetryDelay : delay;
    }

    private async Task<IAsyncDisposable?> LockAsync(string scanId)
    {
        var distributedLock = _serviceProvider.GetService<IDistributedLock>();
        if (distributedLock is null)
        {
            return NoLock.Instance;
        }

        var (locker, locked) = await distributedLock.TryAcquireLockAsync($"UPLOAD_SCAN_{scanId}", s_lockTimeout, s_lockExpiration);
        if (!locked)
        {
            _logger.LogWarning("Scan '{ScanId}' is busy; it is picked up again later.", scanId);
            return null;
        }

        return locker;
    }

    private sealed class NoLock : IAsyncDisposable
    {
        public static readonly NoLock Instance = new();

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
