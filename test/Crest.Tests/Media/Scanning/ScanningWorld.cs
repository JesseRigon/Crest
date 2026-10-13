#nullable enable

using Crest.Media.Files;
using Crest.Media.Scanning;
using Crest.Media.Scanning.Detection;
using Crest.Media.Scanning.Models;
using Crest.Media.Scanning.Services;
using Crest.Modules;

namespace Crest.Tests.Media.Scanning;

/// <summary>
/// The fixtures of the upload-scanning tests: an in-memory job store and storage provider, fake
/// scanners with scripted verdicts, a recording verdict handler, a settable clock and the
/// tenant's scanning settings.
/// </summary>
public sealed class ScanningWorld
{
    public static readonly byte[] Png = [0x89, (byte)'P', (byte)'N', (byte)'G', 0x0D, 0x0A, 0x1A, 0x0A, 0, 0, 0, 13, (byte)'I', (byte)'H', (byte)'D', (byte)'R'];

    public MemoryJobStore Jobs { get; } = new();

    public MemoryStorage Storage { get; } = new();

    public RecordingVerdictHandler Verdicts { get; } = new();

    public TestClock Clock { get; } = new();

    public UploadScanningOptions Options { get; } = new() { StripImageLocation = false };

    public UploadScanningSettings TenantSettings { get; set; } = new();

    public List<IUploadScanner> Scanners { get; } = [];

    public IFileTypeDetector Detector { get; set; } = new SignatureFileTypeDetector();

    public FakeScanner AddScanner(string name, params ScanVerdict[] verdicts)
    {
        var scanner = new FakeScanner(name, verdicts);
        Scanners.Add(scanner);
        Options.Scanners.Add(name);
        return scanner;
    }

    public ServiceProvider Services()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IMediaVerdictHandler>(Verdicts);
        return services.BuildServiceProvider();
    }

    public ScannerCatalog Catalog(IServiceProvider services)
        => new(Scanners, global::Microsoft.Extensions.Options.Options.Create(Options), new StaticTenantSettings(this), services);

    public ScanCoordinator Coordinator()
    {
        var services = Services();

        return new ScanCoordinator(
            Jobs,
            Catalog(services),
            Detector,
            Storage,
            services,
            global::Microsoft.Extensions.Options.Options.Create(Options),
            Clock,
            NullLogger<ScanCoordinator>.Instance);
    }

    /// <summary>A quarantined upload with its job, as <see cref="UploadQuarantine.SubmitAsync"/> leaves it.</summary>
    public async Task<ScanJob> SubmitAsync(byte[] content, string name, string? claimedType, string contentId = "c1", int version = 1)
    {
        var key = MediaBlobKeys.ForVersion(contentId, version);
        await Storage.WriteAsync(MediaStorageArea.Quarantine, key, new MemoryStream(content));

        var services = Services();
        var job = new ScanJob
        {
            ScanId = $"scan-{contentId}-{version}",
            Tenant = "Default",
            ContentId = contentId,
            Version = version,
            Name = name,
            ClaimedType = claimedType,
            Size = content.Length,
            StorageKey = key,
            Scanners = [.. (await Catalog(services).SelectAsync()).Select(scanner => new ScannerState { Name = scanner })],
            CreatedUtc = Clock.UtcNow,
            NextAttemptUtc = Clock.UtcNow,
        };

        await Jobs.SaveAsync(job, commit: true);

        return job;
    }

    public sealed class FakeScanner(string name, params ScanVerdict[] verdicts) : IUploadScanner
    {
        private readonly Queue<ScanVerdict> _verdicts = new(verdicts);

        public string Name { get; } = name;

        public int Calls { get; private set; }

        public List<ScanRequest> Requests { get; } = [];

        public Task<ScanVerdict> ScanAsync(ScanRequest request, CancellationToken cancellationToken = default)
        {
            Calls++;
            Requests.Add(request);
            return Task.FromResult(_verdicts.Count > 1 ? _verdicts.Dequeue() : _verdicts.Peek());
        }
    }

    public sealed class MemoryJobStore : IScanJobStore
    {
        public Dictionary<string, ScanJob> Jobs { get; } = [];

        public Task<ScanJob?> GetAsync(string scanId, CancellationToken cancellationToken = default)
            => Task.FromResult(Jobs.GetValueOrDefault(scanId));

        public Task SaveAsync(ScanJob job, bool commit, CancellationToken cancellationToken = default)
        {
            Jobs[job.ScanId] = job;
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<string>> ListDueAsync(DateTime now, int max, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<string>>([.. Jobs.Values.Where(job => job.NextAttemptUtc <= now).Take(max).Select(job => job.ScanId)]);
    }

    public sealed class MemoryStorage : IMediaStorageProvider
    {
        public Dictionary<(MediaStorageArea Area, string Key), byte[]> Blobs { get; } = [];

        public string Name => "memory";

        public bool FailCommit { get; set; }

        public async Task WriteAsync(MediaStorageArea area, string key, Stream content, CancellationToken cancellationToken = default)
        {
            using var buffer = new MemoryStream();
            await content.CopyToAsync(buffer, cancellationToken);
            Blobs[(area, key)] = buffer.ToArray();
        }

        public Task<Stream?> OpenReadAsync(MediaStorageArea area, string key, CancellationToken cancellationToken = default)
            => Task.FromResult<Stream?>(Blobs.TryGetValue((area, key), out var bytes) ? new MemoryStream(bytes, writable: false) : null);

        public Task CommitAsync(string key, CancellationToken cancellationToken = default)
        {
            if (FailCommit)
            {
                throw new IOException("Disk full.");
            }

            Blobs[(MediaStorageArea.Committed, key)] = Blobs[(MediaStorageArea.Quarantine, key)];
            Blobs.Remove((MediaStorageArea.Quarantine, key));
            return Task.CompletedTask;
        }

        public Task DeleteAsync(MediaStorageArea area, string key, CancellationToken cancellationToken = default)
        {
            Blobs.Remove((area, key));
            return Task.CompletedTask;
        }

        public Task<Uri?> GetTemporaryReadUriAsync(MediaStorageArea area, string key, TimeSpan lifetime, CancellationToken cancellationToken = default)
            => Task.FromResult<Uri?>(null);

        public bool IsQuarantined(string key) => Blobs.ContainsKey((MediaStorageArea.Quarantine, key));

        public bool IsCommitted(string key) => Blobs.ContainsKey((MediaStorageArea.Committed, key));
    }

    public sealed class RecordingVerdictHandler : IMediaVerdictHandler
    {
        public List<(string ContentId, int Version, ScanVerdict Verdict)> Received { get; } = [];

        public Task OnVerdictAsync(string contentId, int version, ScanVerdict verdict, CancellationToken cancellationToken = default)
        {
            Received.Add((contentId, version, verdict));
            return Task.CompletedTask;
        }
    }

    public sealed class TestClock : IClock
    {
        public DateTime UtcNow { get; set; } = new(2026, 10, 11, 12, 0, 0, DateTimeKind.Utc);

        public ITimeZone[] GetTimeZones() => throw new NotSupportedException();

        public ITimeZone GetTimeZone(string timeZoneId) => throw new NotSupportedException();

        public ITimeZone GetSystemTimeZone() => throw new NotSupportedException();

        public DateTimeOffset ConvertToTimeZone(DateTimeOffset dateTimeOffset, ITimeZone timeZone) => throw new NotSupportedException();
    }

    private sealed class StaticTenantSettings(ScanningWorld world) : ITenantScanningSettings
    {
        public Task<UploadScanningSettings> GetAsync() => Task.FromResult(world.TenantSettings);

        public Task UpdateAsync(string? externalScannerUrl, string? secret, bool bypassHostScanners) => throw new NotSupportedException();
    }
}
