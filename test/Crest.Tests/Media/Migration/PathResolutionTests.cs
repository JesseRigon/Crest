#nullable enable
using Crest.Media.Library;
using Crest.Tests.Media.Files;
using static Crest.Tests.Media.Migration.LibraryWorld;

namespace Crest.Tests.Media.Migration;

/// <summary>Library paths resolve to placements (ruling F1): path, folder chain, placement.</summary>
public class PathResolutionTests
{
    [Fact]
    public async Task APathResolvesThroughItsFolderChain()
    {
        var world = new LibraryWorld();
        var drive = await world.CreateLibraryAsync();
        var docs = await world.FolderAsync(drive.Placement.Id, "docs");
        var sub = await world.FolderAsync(docs.Placement.Id, "sub");
        var file = await world.FileAsync(sub.Placement.Id, "b.txt", "text/plain");
        var library = world.Library();

        Assert.Equal(file.Placement.Id, await library.ResolvePathAsync("docs/sub/b.txt", Token));
        Assert.Equal(file.Placement.Id, await library.ResolvePathAsync("/docs//sub/b.txt", Token));
        Assert.Equal(file.Placement.Id, await library.ResolvePathAsync("docs\\sub\\b.txt", Token));
        Assert.Equal(sub.Placement.Id, await library.ResolvePathAsync("docs/sub", Token));
        Assert.Equal(drive.Placement.Id, await library.ResolvePathAsync(string.Empty, Token));
    }

    [Fact]
    public async Task MissingClimbingAndDifferentlyCasedPathsResolveToNothing()
    {
        var world = new LibraryWorld();
        var drive = await world.CreateLibraryAsync();
        var docs = await world.FolderAsync(drive.Placement.Id, "docs");
        await world.FileAsync(docs.Placement.Id, "a.txt", "text/plain");
        var library = world.Library();

        Assert.Null(await library.ResolvePathAsync("docs/missing.txt", Token));
        Assert.Null(await library.ResolvePathAsync("nowhere/a.txt", Token));
        Assert.Null(await library.ResolvePathAsync("docs/../docs/a.txt", Token));
        Assert.Null(await library.ResolvePathAsync("docs/./a.txt", Token));

        // The media folder on disk compared names exactly; so does the library.
        Assert.Null(await library.ResolvePathAsync("Docs/A.txt", Token));
    }

    [Fact]
    public async Task WithoutALibraryDriveNothingResolves()
    {
        var world = new LibraryWorld();
        var library = world.Library();

        Assert.Null(await library.GetDriveIdAsync(Token));
        Assert.False(await library.IsActiveAsync(Token));
        Assert.Null(await library.ResolvePathAsync("docs/a.txt", Token));
    }

    [Fact]
    public async Task TheLibraryIsActiveOnlyOnceImported()
    {
        var world = new LibraryWorld();
        await world.CreateLibraryAsync(active: false);
        var library = world.Library();

        Assert.False(await library.IsActiveAsync(Token));

        world.State.Document.ImportedUtc = world.Files.Sharing.Clock.UtcNow;
        Assert.True(await library.IsActiveAsync(Token));
    }

    [Fact]
    public async Task APathIsTheInverseOfResolving()
    {
        var world = new LibraryWorld();
        var drive = await world.CreateLibraryAsync();
        var docs = await world.FolderAsync(drive.Placement.Id, "docs");
        var file = await world.FileAsync(docs.Placement.Id, "a.txt", "text/plain");
        var library = world.Library();

        Assert.Equal("docs/a.txt", await library.GetPathAsync(file.Placement.Id, Token));
        Assert.Equal(string.Empty, await library.GetPathAsync(drive.Placement.Id, Token));

        // A placement in another drive has no library path.
        var other = await world.Files.DriveAsync("Other");
        var elsewhere = await world.FileAsync(other.Placement.Id, "x.txt", "text/plain");
        Assert.Null(await library.GetPathAsync(elsewhere.Placement.Id, Token));
    }

    [Fact]
    public async Task AHardLinkedFolderResolvesThroughEachOfItsPlacements()
    {
        var world = new LibraryWorld();
        var drive = await world.CreateLibraryAsync();
        var shared = await world.FolderAsync(drive.Placement.Id, "shared");
        var team = await world.FolderAsync(drive.Placement.Id, "team");
        var file = await world.FileAsync(shared.Placement.Id, "a.txt", "text/plain");
        var link = await world.Files.Service(FilesWorld.Root).LinkAsync(shared.Placement.Id, team.Placement.Id, cancellationToken: Token);
        var library = world.Library();

        // The folder's children belong to its content, so both paths reach the same file.
        Assert.Equal(file.Placement.Id, await library.ResolvePathAsync("shared/a.txt", Token));
        Assert.Equal(file.Placement.Id, await library.ResolvePathAsync("team/shared/a.txt", Token));
        Assert.Equal(link.Placement.Id, await library.ResolvePathAsync("team/shared", Token));
    }

    [Fact]
    public async Task ASoftLinkOnTheWayOpensItsTarget()
    {
        var world = new LibraryWorld();
        var drive = await world.CreateLibraryAsync();
        var target = await world.FolderAsync(drive.Placement.Id, "target");
        var file = await world.FileAsync(target.Placement.Id, "a.txt", "text/plain");
        await world.Files.Service(FilesWorld.Root).ShortcutAsync(target.Placement.Id, drive.Placement.Id, "shortcut", Token);
        var library = world.Library();

        Assert.Equal(file.Placement.Id, await library.ResolvePathAsync("shortcut/a.txt", Token));
    }

    [Fact]
    public async Task ARenameIsSeenAtOnceDespiteTheCache()
    {
        var world = new LibraryWorld();
        var drive = await world.CreateLibraryAsync();
        var file = await world.FileAsync(drive.Placement.Id, "old.txt", "text/plain");
        var library = world.Library();
        Assert.Equal(file.Placement.Id, await library.ResolvePathAsync("old.txt", Token));

        await world.Files.Service(FilesWorld.Root).RenameAsync(file.Placement.Id, "new.txt", Token);

        Assert.Null(await library.ResolvePathAsync("old.txt", Token));
        Assert.Equal(file.Placement.Id, await library.ResolvePathAsync("new.txt", Token));
    }

    [Theory]
    [InlineData("a/b/c.txt", "a/b", "c.txt")]
    [InlineData("/c.txt", "", "c.txt")]
    [InlineData("a\\c.txt", "a", "c.txt")]
    public void PathsSplitIntoFolderAndName(string path, string parent, string name)
    {
        Assert.Equal((parent, name), MediaLibraryPaths.SplitLast(path));
    }
}
