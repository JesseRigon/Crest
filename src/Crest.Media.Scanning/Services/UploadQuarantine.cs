using Crest.Entities;
using Crest.Environment.Shell;
using Crest.Environment.Shell.Scope;
using Crest.Media.Files;
using Crest.Media.Scanning.Models;
using Crest.Modules;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Crest.Media.Scanning.Services;

/// <summary>
/// The quarantine flow (docs/media.md § 2, requirements 86-95): every upload is written to the
/// storage provider's quarantine area, recorded as a scan job in its tenant's store, and scanned on
/// the server after the request, never on the client. The verdict commits or rejects it.
/// </summary>
public sealed class UploadQuarantine : IUploadQuarantine
{
    private readonly IMediaStorageProvider _storage;
    private readonly IScanJobStore _store;
    private readonly ScannerCatalog _catalog;
    private readonly ShellSettings _shellSettings;
    private readonly IIdGenerator _idGenerator;
    private readonly IClock _clock;
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger _logger;

    public UploadQuarantine(
        IMediaStorageProvider storage,
        IScanJobStore store,
        ScannerCatalog catalog,
        ShellSettings shellSettings,
        IIdGenerator idGenerator,
        IClock clock,
        IServiceProvider serviceProvider,
        ILogger<UploadQuarantine> logger)
    {
        _storage = storage;
        _store = store;
        _catalog = catalog;
        _shellSettings = shellSettings;
        _idGenerator = idGenerator;
        _clock = clock;
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    public async Task<string> SubmitAsync(string contentId, int version, string name, string? claimedType, Stream content, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(contentId);
        ArgumentNullException.ThrowIfNull(content);

        var key = MediaBlobKeys.ForVersion(contentId, version);
        var counting = new CountingStream(content);

        await _storage.WriteAsync(MediaStorageArea.Quarantine, key, counting, cancellationToken);

        var now = _clock.UtcNow;
        var job = new ScanJob
        {
            ScanId = _idGenerator.GenerateUniqueId(),
            Tenant = _shellSettings.Name,
            ContentId = contentId,
            Version = version,
            Name = name ?? string.Empty,
            ClaimedType = claimedType,
            Size = counting.BytesRead,
            StorageKey = key,
            Status = ScanOutcome.Pending,
            Scanners = [.. (await _catalog.SelectAsync()).Select(scanner => new ScannerState { Name = scanner })],
            CreatedUtc = now,

            // Picked up by the background task if the deferred run below never happens.
            NextAttemptUtc = now,
        };

        // Committed with the caller's upload: the deferred run starts after it.
        await _store.SaveAsync(job, commit: false, cancellationToken);

        var scanId = job.ScanId;
        ShellScope.AddDeferredTask(scope => scope.ServiceProvider.GetRequiredService<ScanCoordinator>().RunAsync(scanId));

        if (_logger.IsEnabled(LogLevel.Debug))
        {
            _logger.LogDebug("Quarantined '{ContentId}' version {Version} as scan '{ScanId}' for {Scanners}.", contentId, version, scanId, string.Join(", ", job.Scanners.Select(s => s.Name)));
        }

        return scanId;
    }

    public async Task ReportVerdictAsync(string scanId, string scannerName, ScanVerdict verdict, CancellationToken cancellationToken = default)
    {
        if (!await _serviceProvider.GetRequiredService<ScanCoordinator>().ReportAsync(scanId, scannerName, verdict, cancellationToken))
        {
            throw new InvalidOperationException($"Scan '{scanId}' has no scanner '{scannerName}' to report on.");
        }
    }

    /// <summary>Counts the bytes the storage provider reads, for the job's size, whatever the stream supports.</summary>
    private sealed class CountingStream(Stream inner) : Stream
    {
        public long BytesRead { get; private set; }

        public override bool CanRead => true;

        public override bool CanSeek => false;

        public override bool CanWrite => false;

        public override long Length => throw new NotSupportedException();

        public override long Position
        {
            get => BytesRead;
            set => throw new NotSupportedException();
        }

        public override int Read(byte[] buffer, int offset, int count) => Count(inner.Read(buffer, offset, count));

        public override int Read(Span<byte> buffer) => Count(inner.Read(buffer));

        public override async Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
            => Count(await inner.ReadAsync(buffer.AsMemory(offset, count), cancellationToken));

        public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
            => Count(await inner.ReadAsync(buffer, cancellationToken));

        public override void Flush()
        {
        }

        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

        public override void SetLength(long value) => throw new NotSupportedException();

        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

        private int Count(int read)
        {
            BytesRead += read;
            return read;
        }
    }
}
