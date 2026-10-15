#nullable enable
using Grove.Media.Files;
using Grove.Media.Library;
using Grove.Tests.Media.Files;
using static Grove.Tests.Media.Migration.LibraryWorld;

namespace Grove.Tests.Media.Migration;

/// <summary>
/// A Tus upload addressed by <c>destinationPath</c> (ruling F1): resolved to its library folder and
/// uploaded through the file-object API, so it lands in quarantine; nothing is written unscanned.
/// </summary>
public class TusRoutingTests
{
    private static MemoryStream Bytes(string text = "bytes") => new(System.Text.Encoding.UTF8.GetBytes(text));

    [Fact]
    public async Task ADestinationPathLandsInItsLibraryFolderInQuarantine()
    {
        var world = new LibraryWorld();
        world.Files.Quarantine = new FilesWorld.RecordingQuarantine(world.Files.Storage);
        var drive = await world.CreateLibraryAsync();
        var images = await world.FolderAsync(drive.Placement.Id, "images");
        var files = world.Files.Service(FilesWorld.Root);

        var item = await MediaLibraryUploads.UploadAsync(world.Library(), files, _ => Task.FromResult(false), "images/new.png", Bytes(), "image/png", Token);

        Assert.NotNull(item);
        Assert.Equal(images.Placement.Id, item.Placement.ParentId);
        Assert.Equal("new.png", item.Placement.Name);
        Assert.Equal(MediaVersionState.Quarantined, Assert.Single(item.Content.Versions).State);
        Assert.Contains((item.Content.Id, 1), world.Files.Quarantine.Submitted);
        Assert.Equal(item.Placement.Id, await world.Library().ResolvePathAsync("images/new.png", Token));

        // Nothing is servable before the verdict.
        Assert.Null(await files.OpenReadAsync(item.Placement.Id, cancellationToken: Token));
    }

    [Fact]
    public async Task AMissingFolderIsCreatedThroughTheStoreFirst()
    {
        var world = new LibraryWorld();
        var drive = await world.CreateLibraryAsync();
        var files = world.Files.Service(FilesWorld.Root);
        var created = new List<string>();

        async Task<bool> CreateAsync(string path)
        {
            created.Add(path);
            await files.CreateFolderAsync(drive.Placement.Id, path, Token);
            return true;
        }

        var item = await MediaLibraryUploads.UploadAsync(world.Library(), files, CreateAsync, "uploads/a.txt", Bytes(), "text/plain", Token);

        Assert.Equal(["uploads"], created);
        Assert.Equal(await world.Library().ResolvePathAsync("uploads", Token), item?.Placement.ParentId);
    }

    [Fact]
    public async Task AFolderTheUploaderCannotReachIsNotFound()
    {
        var world = new LibraryWorld();
        await world.CreateLibraryAsync();
        var files = world.Files.Service(FilesWorld.Root);

        await Assert.ThrowsAsync<MediaObjectNotFoundException>(() =>
            MediaLibraryUploads.UploadAsync(world.Library(), files, _ => Task.FromResult(false), "nowhere/a.txt", Bytes(), "text/plain", Token));
    }

    [Fact]
    public async Task UploadsBeforeTheLibraryIsActiveAreLeftToTheMediaFolder()
    {
        var world = new LibraryWorld();
        await world.CreateLibraryAsync(active: false);
        var files = world.Files.Service(FilesWorld.Root);

        Assert.Null(await MediaLibraryUploads.UploadAsync(world.Library(), files, _ => Task.FromResult(false), "a.txt", Bytes(), "text/plain", Token));
        Assert.Single(world.Files.Store.Placements);
    }

    [Theory]
    [InlineData("/abc", "abc", null)]
    [InlineData("/abc/v/3", "abc", 3)]
    public void FilesPathsAreParsedAsTheByteEndpointRoutesThem(string rest, string id, int? version)
    {
        Assert.True(MediaLibraryRequestMiddleware.TryParseFiles(rest, out var placementId, out var parsed));
        Assert.Equal(id, placementId);
        Assert.Equal(version, parsed);
        Assert.False(MediaLibraryRequestMiddleware.TryParseFiles("/abc/x/3", out _, out _));
    }
}
