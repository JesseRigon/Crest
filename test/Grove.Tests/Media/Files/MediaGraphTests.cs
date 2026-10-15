#nullable enable
using Grove.Media.Files;
using static Grove.Tests.Media.Files.FilesWorld;

namespace Grove.Tests.Media.Files;

/// <summary>The placement graph and its loop gate (ruling F4): the closure is baked, folders
/// hard-link like files, and no folder ever ends up inside itself through any placement.</summary>
public class MediaGraphTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task AncestorsRunNearestFirstUpToTheDrive()
    {
        var world = new FilesWorld();
        var files = world.Service(Root);
        var drive = await world.DriveAsync();
        var a = await files.CreateFolderAsync(drive.Placement.Id, "a", Token);
        var b = await files.CreateFolderAsync(a.Placement.Id, "b", Token);
        var file = await files.UploadAsync(Upload(b.Placement.Id, "f.txt"), Token);

        Assert.Equal([b.Placement.Id, a.Placement.Id, drive.Placement.Id], await world.Graph().AncestorsAsync(file.Placement.Id, Token));
        Assert.Equal(drive.Placement.Id, file.Placement.DriveId);
    }

    [Fact]
    public async Task AFolderCannotMoveIntoItselfOrBelowItself()
    {
        var world = new FilesWorld();
        var files = world.Service(Root);
        var drive = await world.DriveAsync();
        var a = await files.CreateFolderAsync(drive.Placement.Id, "a", Token);
        var b = await files.CreateFolderAsync(a.Placement.Id, "b", Token);

        await Assert.ThrowsAsync<MediaObjectConflictException>(() => files.MoveAsync(a.Placement.Id, a.Placement.Id, Token));
        await Assert.ThrowsAsync<MediaObjectConflictException>(() => files.MoveAsync(a.Placement.Id, b.Placement.Id, Token));
        Assert.Equal(drive.Placement.Id, world.Store.Placements[a.Placement.Id].Placement.ParentId);
    }

    [Fact]
    public async Task AHardLinkedFolderShowsTheSameChildrenEverywhere()
    {
        var world = new FilesWorld();
        var files = world.Service(Root);
        var drive = await world.DriveAsync();
        var a = await files.CreateFolderAsync(drive.Placement.Id, "a", Token);
        var x = await files.CreateFolderAsync(drive.Placement.Id, "x", Token);
        var file = await files.UploadAsync(Upload(a.Placement.Id, "f.txt"), Token);

        var linked = await files.LinkAsync(a.Placement.Id, x.Placement.Id, cancellationToken: Token);

        Assert.Equal(a.Content.Id, linked.Content.Id);
        Assert.Equal([file.Placement.Id], (await files.ListChildrenAsync(linked.Placement.Id, Token)).Select(item => item.Placement.Id));

        // The file is below both placements of the folder.
        var ancestors = await world.Graph().AncestorsAsync(file.Placement.Id, Token);
        Assert.Contains(a.Placement.Id, ancestors);
        Assert.Contains(linked.Placement.Id, ancestors);
        Assert.Contains(x.Placement.Id, ancestors);
    }

    [Fact]
    public async Task TheLoopGateSeesEveryPlacementOfTheDestination()
    {
        // a is in the drive and, hard-linked, inside x. Putting x into a (through either
        // placement of a) would put x inside itself.
        var world = new FilesWorld();
        var files = world.Service(Root);
        var drive = await world.DriveAsync();
        var a = await files.CreateFolderAsync(drive.Placement.Id, "a", Token);
        var x = await files.CreateFolderAsync(drive.Placement.Id, "x", Token);
        await files.LinkAsync(a.Placement.Id, x.Placement.Id, cancellationToken: Token);

        Assert.True(await world.Graph().WouldLoopAsync(x.Content.Id, a.Placement.Id, Token));
        await Assert.ThrowsAsync<MediaObjectConflictException>(() => files.MoveAsync(x.Placement.Id, a.Placement.Id, Token));
        await Assert.ThrowsAsync<MediaObjectConflictException>(() => files.LinkAsync(x.Placement.Id, a.Placement.Id, cancellationToken: Token));
    }

    [Fact]
    public async Task AFolderCannotBeHardLinkedIntoItself()
    {
        var world = new FilesWorld();
        var files = world.Service(Root);
        var drive = await world.DriveAsync();
        var a = await files.CreateFolderAsync(drive.Placement.Id, "a", Token);
        var b = await files.CreateFolderAsync(a.Placement.Id, "b", Token);

        await Assert.ThrowsAsync<MediaObjectConflictException>(() => files.LinkAsync(a.Placement.Id, b.Placement.Id, cancellationToken: Token));
    }

    [Fact]
    public async Task AShortcutToAnAncestorIsNotALoop()
    {
        var world = new FilesWorld();
        var files = world.Service(Root);
        var drive = await world.DriveAsync();
        var a = await files.CreateFolderAsync(drive.Placement.Id, "a", Token);
        var b = await files.CreateFolderAsync(a.Placement.Id, "b", Token);

        var shortcut = await files.ShortcutAsync(a.Placement.Id, b.Placement.Id, cancellationToken: Token);

        Assert.True(shortcut.Placement.IsSoftLink);
        Assert.Equal([b.Placement.Id, a.Placement.Id, drive.Placement.Id], await world.Graph().AncestorsAsync(shortcut.Placement.Id, Token));
    }

    [Fact]
    public async Task MovingAFolderRecalculatesEverythingBelowIt()
    {
        var world = new FilesWorld();
        var files = world.Service(Root);
        var first = await world.DriveAsync("First");
        var second = await world.DriveAsync("Second");
        var a = await files.CreateFolderAsync(first.Placement.Id, "a", Token);
        var file = await files.UploadAsync(Upload(a.Placement.Id, "f.txt"), Token);
        var commits = world.Store.Commits;

        await files.MoveAsync(a.Placement.Id, second.Placement.Id, Token);

        Assert.Equal([a.Placement.Id, second.Placement.Id], await world.Graph().AncestorsAsync(file.Placement.Id, Token));
        Assert.Equal(second.Placement.Id, world.Store.Placements[file.Placement.Id].Placement.DriveId);

        // Committed under the lock.
        Assert.True(world.Store.Commits > commits);
    }

    [Fact]
    public async Task RemovingOnePlacementOfAFolderKeepsItsChildrenUnderTheOther()
    {
        var world = new FilesWorld();
        var files = world.Service(Root);
        var drive = await world.DriveAsync();
        var a = await files.CreateFolderAsync(drive.Placement.Id, "a", Token);
        var x = await files.CreateFolderAsync(drive.Placement.Id, "x", Token);
        var file = await files.UploadAsync(Upload(a.Placement.Id, "f.txt"), Token);
        var linked = await files.LinkAsync(a.Placement.Id, x.Placement.Id, cancellationToken: Token);

        await files.DeleteAsync(a.Placement.Id, Token);

        Assert.True(world.Store.Contents.ContainsKey(a.Content.Id));
        var child = world.Store.Placements[file.Placement.Id];
        Assert.Equal(linked.Placement.Id, child.Placement.ParentId);
        Assert.Equal([linked.Placement.Id, x.Placement.Id, drive.Placement.Id], await world.Graph().AncestorsAsync(file.Placement.Id, Token));
    }
}
