#nullable enable
using Grove.Media;
using Grove.Media.Files;
using Grove.Media.Sharing;
using Grove.Access.Sharing;
using static Grove.Tests.Media.Files.FilesWorld;
using DriveType = Grove.Media.Files.DriveType;

namespace Grove.Tests.Media.Files;

/// <summary>File objects as the "file" shareable kind: placements form the tree up to their
/// drive, soft links resolve to their target, and the media permissions map to levels.</summary>
public class MediaFileShareableKindTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task APlacementInheritsFromItsFolderUpToItsDrive()
    {
        var world = new FilesWorld();
        var files = world.Service(Root);
        var drive = await world.DriveAsync();
        var folder = await files.CreateFolderAsync(drive.Placement.Id, "Images", Token);
        var file = await files.UploadAsync(Upload(folder.Placement.Id, "Chair.JPG", type: "image/jpeg"), Token);
        await files.UpdateMetadataAsync(file.Placement.Id, new MediaMetadata(Tags: ["final"]), Token);

        var shared = await world.Kind.GetAsync(file.Placement.Id, Token);
        var root = await world.Kind.GetAsync(drive.Placement.Id, Token);

        Assert.Equal(new ShareableRef(MediaFileShareableKind.KindName, folder.Placement.Id), shared!.Parent);
        Assert.True(root!.IsRoot);
        Assert.Null(shared.OwnerUserId);
        Assert.Equal(["jpg"], shared.Facts[MediaFileShareableKind.Facts.Extension]);
        Assert.Equal(["image/jpeg"], shared.Facts[MediaFileShareableKind.Facts.MimeType]);
        Assert.Equal(["final"], shared.Facts[MediaFileShareableKind.Facts.Tag]);
        Assert.Equal(["file"], shared.Facts[MediaFileShareableKind.Facts.Kind]);
        Assert.Equal(["drive"], root.Facts[MediaFileShareableKind.Facts.Kind]);
    }

    [Fact]
    public async Task ItRecognisesPlacementsAndItemsAndASoftLinkIsItsTarget()
    {
        var world = new FilesWorld();
        var files = world.Service(Root);
        var drive = await world.DriveAsync();
        var file = await files.UploadAsync(Upload(drive.Placement.Id, "f.txt"), Token);
        var shortcut = await files.ShortcutAsync(file.Placement.Id, drive.Placement.Id, "link", Token);

        Assert.True(world.Kind.TryIdentify(file.Placement, out var byPlacement));
        Assert.True(world.Kind.TryIdentify(file, out var byItem));
        Assert.True(world.Kind.TryIdentify(shortcut.Placement, out var byShortcut));
        Assert.False(world.Kind.TryIdentify("images/f.txt", out _));
        Assert.Equal(file.Placement.Id, byPlacement);
        Assert.Equal(file.Placement.Id, byItem);
        Assert.Equal(file.Placement.Id, byShortcut);
    }

    [Fact]
    public async Task APersonalDrivesUserOwnsItAndWhatIsInIt()
    {
        var world = new FilesWorld();
        var pat = world.Service(User("pat"));
        var drive = await pat.CreateDriveAsync("Mine", DriveType.Personal, MediaOwner.User("pat"), Token);
        var file = await pat.UploadAsync(Upload(drive.Placement.Id, "f.txt"), Token);

        Assert.Equal("pat", (await world.Kind.GetAsync(drive.Placement.Id, Token))!.OwnerUserId);
        Assert.Equal("pat", (await world.Kind.GetAsync(file.Placement.Id, Token))!.OwnerUserId);
        Assert.Equal(AccessLevel.Admin, (await world.Resolver().ResolveAsync(User("pat"), new ShareableRef(MediaFileShareableKind.KindName, file.Placement.Id), Token)).Level);
    }

    [Fact]
    public async Task EverythingInAnInactiveDriveIsInactive()
    {
        var world = new FilesWorld();
        var drive = await world.DriveAsync();
        var file = await world.Service(Root).UploadAsync(Upload(drive.Placement.Id, "f.txt"), Token);
        world.Store.Contents[drive.Content.Id].Content.IsInactive = true;

        Assert.True((await world.Kind.GetAsync(file.Placement.Id, Token))!.IsInactive);
    }

    [Fact]
    public void TheMediaPermissionsMapToLevels()
    {
        var kind = new FilesWorld().Kind;

        Assert.Equal(AccessLevel.Viewer, kind.LevelFor(MediaPermissions.ViewMedia.Name));
        Assert.Equal(AccessLevel.Editor, kind.LevelFor(MediaPermissions.EditMedia.Name));
        Assert.Equal(AccessLevel.Manager, kind.LevelFor(MediaPermissions.ManageMedia.Name));
        Assert.Equal(AccessLevel.Manager, kind.LevelFor(MediaPermissions.ManageOwnMedia.Name));
        Assert.Equal(AccessLevel.Manager, kind.LevelFor(MediaPermissions.ManageOthersMedia.Name));
        Assert.Equal(AccessLevel.Manager, kind.LevelFor(MediaPermissions.ManageMediaFolder.Name));
        Assert.Null(kind.LevelFor(MediaPermissions.ManageSharedDrives.Name));
    }

    [Fact]
    public async Task EachPlacementOfAHardLinkedFileHasItsOwnSharing()
    {
        var world = new FilesWorld();
        var files = world.Service(Root);
        var drive = await world.DriveAsync();
        var open = await files.CreateFolderAsync(drive.Placement.Id, "open", Token);
        var closed = await files.CreateFolderAsync(drive.Placement.Id, "closed", Token);
        var file = await files.UploadAsync(Upload(closed.Placement.Id, "f.txt"), Token);
        var link = await files.LinkAsync(file.Placement.Id, open.Placement.Id, cancellationToken: Token);
        world.Share(open.Placement.Id, ShareEntry.Grant(SharePrincipal.Tenant, AccessLevel.Viewer));

        var reader = world.Service(User("ray"));
        Assert.NotNull(await reader.GetAsync(link.Placement.Id, Token));
        Assert.Null(await reader.GetAsync(file.Placement.Id, Token));
    }
}
