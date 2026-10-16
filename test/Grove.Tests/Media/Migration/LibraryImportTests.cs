#nullable enable
using System.Security.Claims;
using System.Text.Json.Nodes;
using Grove.Access;
using Grove.Access.Services;
using Grove.Data.Storage.FileSystem;
using Grove.Media;
using Grove.Media.Files;
using Grove.Media.Library;
using Grove.Media.Services;
using Grove.Security;
using Grove.Security.Permissions;
using Grove.Security.Services;
using Grove.Access.Sharing;
using Grove.Tests.Media.Files;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using static Grove.Tests.Media.Migration.LibraryWorld;
using DriveType = Grove.Media.Files.DriveType;

namespace Grove.Tests.Media.Migration;

/// <summary>
/// The one-time import of a small <c>/media</c> tree (ruling F1): a library drive, folders and
/// files as file objects through the quarantine, the library activated, fields rewritten to ids.
/// </summary>
public sealed class LibraryImportTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "media-import-" + Guid.NewGuid().ToString("n"));

    public LibraryImportTests()
    {
        Write("logo.png", "png");
        Write("docs/a.txt", "a");
        Write("docs/sub/b.txt", "b");
        Directory.CreateDirectory(Path.Combine(_root, "empty"));
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    private void Write(string path, string text)
    {
        var full = Path.Combine(_root, path.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(full)!);
        File.WriteAllText(full, text);
    }

    private (MediaLibraryImporter Importer, RecordingRewriter Rewriter) Importer(LibraryWorld world, bool trustExisting = false, Action<IServiceCollection>? configure = null)
    {
        var files = world.Files.Service(SystemCaller);
        var services = new ServiceCollection()
            .AddSingleton<ICallerContextFactory>(new SystemCallers())
            .AddSingleton<IAccessRunner>(new AccessRunner(new FilesWorld.Accessor(null)))
            .AddSingleton<IMediaVerdictHandler>(files);
        configure?.Invoke(services);
        var provider = services.BuildServiceProvider();
        var rewriter = new RecordingRewriter();
        var legacy = new LibraryFileStore(new FileSystemStore(_root, NullLogger<FileSystemStore>.Instance));
        var importer = new MediaLibraryImporter(
            provider,
            world.State,
            files,
            world.Files.Store,
            new StorePlacementNameLookup(world.Files.Store),
            legacy,
            new MediaLibrarySharingImport(provider, Options.Create(new MediaOptions { AssetsUsersFolder = "_Users" })),
            rewriter,
            world.Files.Sharing.Clock,
            Options.Create(new MediaLibraryOptions { TrustExistingFiles = trustExisting }),
            NullLogger<MediaLibraryImporter>.Instance);

        return (importer, rewriter);
    }

    [Fact]
    public async Task ASmallTreeBecomesFileObjectsInTheLibraryDrive()
    {
        var world = new LibraryWorld();
        world.Files.Quarantine = new FilesWorld.RecordingQuarantine(world.Files.Storage);
        var (importer, rewriter) = Importer(world);

        var report = await importer.ImportAsync(cancellationToken: Token);

        Assert.Equal(3, report.FoldersCreated);
        Assert.Equal(3, report.FilesImported);
        Assert.Empty(report.Failures);
        Assert.Equal(1, rewriter.Runs);

        var library = world.Library();
        Assert.True(await library.IsActiveAsync(Token));
        var driveId = await library.GetDriveIdAsync(Token);
        var drive = world.Files.Store.Placements[driveId!];
        Assert.Equal(MediaLibraryImporter.DriveName, drive.Placement.Name);
        Assert.Equal(DriveType.Shared, world.Files.Store.Contents[drive.Placement.ContentId].Content.DriveType);

        foreach (var path in new[] { "logo.png", "docs/a.txt", "docs/sub/b.txt" })
        {
            var placementId = await library.ResolvePathAsync(path, Token);
            Assert.NotNull(placementId);
            var content = world.Files.Store.Contents[world.Files.Store.Placements[placementId].Placement.ContentId].Content;
            Assert.Equal(MediaObjectKind.File, content.Kind);

            // The existing bytes are version 1, held in quarantine until scanned.
            var version = Assert.Single(content.Versions);
            Assert.Equal(MediaVersionState.Quarantined, version.State);
            Assert.Contains((content.Id, 1), world.Files.Quarantine.Submitted);
        }

        var empty = await library.ResolvePathAsync("empty", Token);
        Assert.Equal(MediaObjectKind.Folder, world.Files.Store.Contents[world.Files.Store.Placements[empty!].Placement.ContentId].Content.Kind);
    }

    [Fact]
    public async Task TrustedFilesAreCleanAsImportedPreExisting()
    {
        var world = new LibraryWorld();
        world.Files.Quarantine = new FilesWorld.RecordingQuarantine(world.Files.Storage);
        var (importer, _) = Importer(world, trustExisting: true);

        await importer.ImportAsync(cancellationToken: Token);

        var placementId = await world.Library().ResolvePathAsync("docs/a.txt", Token);
        var content = world.Files.Store.Contents[world.Files.Store.Placements[placementId!].Placement.ContentId].Content;
        var version = Assert.Single(content.Versions);
        Assert.Equal(MediaVersionState.Clean, version.State);
        Assert.Equal(MediaLibraryImporter.TrustedReason, version.Reason);
        Assert.Equal(1, content.PublishedVersion);
        Assert.True(world.State.Document.TrustedExisting);

        // Trusted or scanned, never both.
        Assert.Empty(world.Files.Quarantine.Submitted);
    }

    [Fact]
    public async Task RunningAgainKeepsWhatIsThere()
    {
        var world = new LibraryWorld();
        var (importer, _) = Importer(world);
        await importer.ImportAsync(cancellationToken: Token);
        var placements = world.Files.Store.Placements.Count;
        var driveId = world.State.Document.DrivePlacementId;

        Write("docs/new.txt", "new");
        var report = await importer.ImportAsync(cancellationToken: Token);

        Assert.Equal(driveId, world.State.Document.DrivePlacementId);
        Assert.Equal(0, report.FoldersCreated);
        Assert.Equal(1, report.FilesImported);
        Assert.Equal(3, report.Skipped);
        Assert.Equal(placements + 1, world.Files.Store.Placements.Count);
    }

    [Fact]
    public async Task FieldPathsAreRewrittenToPlacementIds()
    {
        var world = new LibraryWorld();
        var (importer, _) = Importer(world);
        await importer.ImportAsync(cancellationToken: Token);
        var library = world.Library();
        var logo = await library.ResolvePathAsync("logo.png", Token);
        var a = await library.ResolvePathAsync("docs/a.txt", Token);

        var content = new JsonObject
        {
            ["Article"] = new JsonObject
            {
                ["Image"] = new JsonObject { ["Paths"] = new JsonArray("logo.png", "missing.png"), ["MediaTexts"] = new JsonArray("Logo", "") },
                ["Title"] = new JsonObject { ["Text"] = "Paths" },
            },
            ["Slides"] = new JsonObject
            {
                ["ContentItems"] = new JsonArray(new JsonObject
                {
                    ["Slide"] = new JsonObject { ["Image"] = new JsonObject { ["Paths"] = new JsonArray("docs/a.txt") } },
                }),
            },
            ["SeoMeta"] = new JsonObject { ["OpenGraphImage"] = new JsonObject { ["Paths"] = new JsonArray("logo.png") } },
        };

        var names = new HashSet<string>(MediaFieldRewriter.KnownPropertyFields) { "Image" };
        Task<bool> Exists(string id) => Task.FromResult(world.Files.Store.Placements.ContainsKey(id));
        var changed = await MediaFieldRewriter.RewriteAsync(content, names, path => library.ResolvePathAsync(path, Token), Exists);

        Assert.True(changed);
        Assert.Equal([logo, string.Empty], Ids(content["Article"]!["Image"]!));
        Assert.Equal([a], Ids(content["Slides"]!["ContentItems"]![0]!["Slide"]!["Image"]!));
        Assert.Equal([logo], Ids(content["SeoMeta"]!["OpenGraphImage"]!));

        // Paths stay beside the ids, and a second pass changes nothing.
        Assert.Equal("logo.png", content["Article"]!["Image"]!["Paths"]![0]!.GetValue<string>());
        Assert.False(await MediaFieldRewriter.RewriteAsync(content, names, path => library.ResolvePathAsync(path, Token), Exists));

        // An id from another tenant is replaced by this tenant's placement for the path.
        content["SeoMeta"]!["OpenGraphImage"]!["PlacementIds"] = new JsonArray("0123456789abcdefghjkmnpqrs");
        Assert.True(await MediaFieldRewriter.RewriteAsync(content, names, path => library.ResolvePathAsync(path, Token), Exists));
        Assert.Equal([logo], Ids(content["SeoMeta"]!["OpenGraphImage"]!));
    }

    [Fact]
    public async Task WithoutSecureMediaTheLibraryIsSharedWithAnyone()
    {
        var world = new LibraryWorld();
        var drive = await world.CreateLibraryAsync();
        var sharing = new MediaLibrarySharingImport(new ServiceCollection().BuildServiceProvider(), Options.Create(new MediaOptions()));

        var grant = Assert.Single(await sharing.SecureMediaGrantsAsync(drive.Placement.Id, []));

        Assert.Equal(drive.Placement.Id, grant.PlacementId);
        Assert.Equal(ShareEntry.Grant(SharePrincipal.Anyone, AccessLevel.Viewer), grant.Entry);
    }

    [Fact]
    public async Task SecureMediaFolderRightsBecomeViewerGrants()
    {
        var world = new LibraryWorld();
        var roles = new FakeRoles(new()
        {
            ["Anonymous"] = [$"{MediaPermissions.ViewMedia.Name}_public"],
            ["Authenticated"] = [MediaPermissions.ViewRootMedia.Name],
            ["Staff"] = [MediaPermissions.ViewMedia.Name],
            ["Support"] = [MediaPermissions.ViewOthersMedia.Name],
        });
        var services = new ServiceCollection()
            .AddSingleton(new SecureMediaMarker())
            .AddSingleton<IRoleService>(roles)
            .BuildServiceProvider();
        var sharing = new MediaLibrarySharingImport(services, Options.Create(new MediaOptions { AssetsUsersFolder = "_Users" }));
        ImportedPlacement[] imported =
        [
            new(string.Empty, "drive", IsFolder: true),
            new("public", "public-folder", IsFolder: true),
            new("private", "private-folder", IsFolder: true),
            new("_Users", "users-folder", IsFolder: true),
            new("logo.png", "logo", IsFolder: false),
            new("public/a.png", "public-a", IsFolder: false),
        ];

        var grants = await sharing.SecureMediaGrantsAsync("drive", imported);

        Assert.Equal(
            [
                ("public-folder", ShareEntry.Grant(SharePrincipal.Anyone, AccessLevel.Viewer)),
                ("logo", ShareEntry.Grant(SharePrincipal.Tenant, AccessLevel.Viewer)),
                ("drive", ShareEntry.Grant(SharePrincipal.Role("Staff"), AccessLevel.Viewer)),
                ("users-folder", ShareEntry.Grant(SharePrincipal.Role("Support"), AccessLevel.Viewer)),
            ],
            grants);
    }

    private static string?[] Ids(JsonNode field) =>
        [.. field["PlacementIds"]!.AsArray().Select(id => id!.GetValue<string>())];

    private sealed class RecordingRewriter : IMediaFieldRewriter
    {
        public int Runs { get; private set; }

        public Task<int> RewriteAsync(CancellationToken cancellationToken = default)
        {
            Runs++;
            return Task.FromResult(0);
        }

        public Task<bool> RewriteAsync(global::Grove.Contents.Management.ContentItem contentItem, CancellationToken cancellationToken = default) => Task.FromResult(false);
    }

    private sealed class FakeRoles(Dictionary<string, string[]> permissions) : IRoleService
    {
        public Task<IEnumerable<IRole>> GetRolesAsync() =>
            Task.FromResult<IEnumerable<IRole>>([.. permissions.Keys.Select(name => new Role { RoleName = name })]);

        public Task<IEnumerable<Claim>> GetRoleClaimsAsync(string role, CancellationToken cancellationToken = default) =>
            Task.FromResult<IEnumerable<Claim>>([.. permissions[role].Select(permission => new Claim(Permission.ClaimType, permission))]);

        public Task<IEnumerable<string>> GetNormalizedRoleNamesAsync() => Task.FromResult<IEnumerable<string>>([.. permissions.Keys.Select(name => name.ToUpperInvariant())]);
    }
}
