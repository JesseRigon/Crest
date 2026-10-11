#nullable enable
using Crest.Access;
using Crest.Access.Services;
using Crest.Environment.Shell;
using Crest.Media;
using Crest.Media.Files;
using Crest.Media.Library;
using Crest.Sharing;
using Crest.Tests.Media.Files;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Crest.Tests.Media.Migration;

/// <summary>
/// The media library tests' fixtures (ruling F1): the file-object world (in-memory placements and
/// bytes, sharing as the authority) with a "Media library" shared drive, the library over it, and
/// the reference resolver as a given caller.
/// </summary>
public sealed class LibraryWorld
{
    public FilesWorld Files { get; } = new();

    public MemoryLibraryState State { get; } = new();

    public MemoryCache Cache { get; } = new(new MemoryCacheOptions());

    public static readonly HmacMediaUrlSigner Signer = new([.. Enumerable.Range(0, 32).Select(index => (byte)index)]);

    public static readonly CallerContext SystemCaller = new() { Tenant = "Default", Side = CallerSide.System, IsSystem = true, IsSuperUser = true, UserName = "system" };

    public static CancellationToken Token => TestContext.Current.CancellationToken;

    public MediaLibrary Library() => new(Files.Store, new StorePlacementNameLookup(Files.Store), State, Cache);

    /// <summary>The library drive, made by the tenant administrator; active unless asked otherwise.</summary>
    public async Task<MediaItem> CreateLibraryAsync(bool active = true)
    {
        var drive = await Files.DriveAsync(MediaLibraryImporter.DriveName);
        State.Document.DrivePlacementId = drive.Placement.Id;
        State.Document.ImportedUtc = active ? Files.Sharing.Clock.UtcNow : null;
        return drive;
    }

    public async Task<MediaItem> FolderAsync(string parentId, string name) =>
        await Files.Service(FilesWorld.Root).CreateFolderAsync(parentId, name, Token);

    /// <summary>A file, scanned clean.</summary>
    public async Task<MediaItem> FileAsync(string parentId, string name, string type = "image/png")
    {
        var files = Files.Service(FilesWorld.Root);
        var item = await files.UploadAsync(FilesWorld.Upload(parentId, name, type: type), Token);
        await files.OnVerdictAsync(item.Content.Id, 1, ScanVerdict.Clean, Token);
        return item;
    }

    public void SharePublic(string placementId) =>
        Files.Share(placementId, ShareEntry.Grant(SharePrincipal.Anyone, AccessLevel.Viewer));

    public IServiceProvider Services(CallerContext? caller, IMediaFileStore? mediaFileStore = null)
    {
        var accessor = new FilesWorld.Accessor(caller);
        var services = new ServiceCollection()
            .AddSingleton<ICallerContextAccessor>(accessor)
            .AddSingleton<IAccessRunner>(new AccessRunner(accessor))
            .AddSingleton(Files.Resolver())
            .AddSingleton<IMediaUrlSigner>(Signer)
            .AddSingleton(Signer)
            .AddSingleton(new ShellSettings { Name = "Default" });

        if (mediaFileStore is not null)
        {
            services.AddSingleton(mediaFileStore);
        }

        return services.BuildServiceProvider();
    }

    public MediaReferenceResolver Resolver(CallerContext? caller, IMediaFileStore? mediaFileStore = null) => new(
        Files.Service(caller),
        Library(),
        Services(caller, mediaFileStore),
        new HttpContextAccessor(),
        new ShellSettings { Name = "Default" },
        Files.Sharing.Clock,
        Options.Create(new MediaOptions { AssetsRequestPath = "/media" }),
        Options.Create(new MediaLibraryOptions()));

    public sealed class MemoryLibraryState : IMediaLibraryState
    {
        public MediaLibraryDocument Document { get; private set; } = new();

        public Task<MediaLibraryDocument> GetAsync() => Task.FromResult(Document);

        public Task<MediaLibraryDocument> LoadForUpdateAsync() => Task.FromResult(Document);

        public Task SaveAsync(MediaLibraryDocument document)
        {
            Document = document;
            return Task.CompletedTask;
        }
    }

    /// <summary>The system actor, as the access feature builds it.</summary>
    public sealed class SystemCallers : ICallerContextFactory
    {
        public Task<CallerContext> CreateAsync(CallerRequest request, CancellationToken cancellationToken = default) => Task.FromResult(SystemCaller);

        public Task<CallerContext> CreateSystemAsync(string? organizationId = null, CancellationToken cancellationToken = default) => Task.FromResult(SystemCaller);
    }
}
