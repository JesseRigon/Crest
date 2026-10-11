#nullable enable
using Crest.Access;
using Crest.Media.Files;
using Crest.Sharing;
using static Crest.Tests.Media.Files.FilesWorld;
using DriveType = Crest.Media.Files.DriveType;

namespace Crest.Tests.Media.Files;

/// <summary>Placement, link, shortcut and delete semantics, decided through sharing
/// (docs/media.md § 3, docs/sharing.md requirements 68-70).</summary>
public class MediaObjectServiceTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task AHardLinkSharesTheContentAndTheContentGoesWithItsLastPlacement()
    {
        var world = new FilesWorld();
        var files = world.Service(Root);
        var drive = await world.DriveAsync();
        var a = await files.CreateFolderAsync(drive.Placement.Id, "a", Token);
        var b = await files.CreateFolderAsync(drive.Placement.Id, "b", Token);
        var file = await files.UploadAsync(Upload(a.Placement.Id, "f.txt"), Token);
        var link = await files.LinkAsync(file.Placement.Id, b.Placement.Id, "g.txt", Token);

        // Editing through one placement changes it everywhere.
        await files.UpdateMetadataAsync(link.Placement.Id, new MediaMetadata(Title: "Shared"), Token);
        Assert.Equal("Shared", (await files.GetAsync(file.Placement.Id, Token))!.Content.Metadata.Title);

        await files.DeleteAsync(file.Placement.Id, Token);
        Assert.True(world.Store.Contents.ContainsKey(file.Content.Id));
        Assert.NotEmpty(world.Storage.Blobs);

        await files.DeleteAsync(link.Placement.Id, Token);
        Assert.False(world.Store.Contents.ContainsKey(file.Content.Id));
        Assert.Empty(world.Storage.Blobs);
    }

    [Fact]
    public async Task AShortcutOpensItsTargetAndGrantsNothing()
    {
        var world = new FilesWorld();
        var files = world.Service(Root);
        var drive = await world.DriveAsync();
        var secret = await files.CreateFolderAsync(drive.Placement.Id, "secret", Token);
        var open = await files.CreateFolderAsync(drive.Placement.Id, "open", Token);
        var file = await files.UploadAsync(Upload(secret.Placement.Id, "f.txt"), Token);
        var shortcut = await files.ShortcutAsync(file.Placement.Id, open.Placement.Id, cancellationToken: Token);
        world.Share(open.Placement.Id, ShareEntry.Grant(SharePrincipal.User("dana"), AccessLevel.Viewer));

        var dana = world.Service(User("dana"));
        Assert.Null(await dana.GetAsync(shortcut.Placement.Id, Token));
        Assert.Empty(await dana.ListChildrenAsync(open.Placement.Id, Token));

        // Shared on the target, the shortcut opens it.
        world.Share(file.Placement.Id, ShareEntry.Grant(SharePrincipal.User("dana"), AccessLevel.Viewer));
        var opened = await dana.GetAsync(shortcut.Placement.Id, Token);
        Assert.Equal(file.Content.Id, opened!.Content.Id);
        Assert.NotNull(await dana.OpenReadAsync(shortcut.Placement.Id, cancellationToken: Token));
    }

    [Fact]
    public async Task RemovingTheTargetRemovesItsShortcuts()
    {
        var world = new FilesWorld();
        var files = world.Service(Root);
        var drive = await world.DriveAsync();
        var file = await files.UploadAsync(Upload(drive.Placement.Id, "f.txt"), Token);
        var shortcut = await files.ShortcutAsync(file.Placement.Id, drive.Placement.Id, "f link", Token);

        await files.DeleteAsync(file.Placement.Id, Token);

        Assert.False(world.Store.Placements.ContainsKey(shortcut.Placement.Id));
    }

    [Fact]
    public async Task DeletingAFolderNeedsManagerOnEverythingBelowAndNamesNothing()
    {
        var world = new FilesWorld();
        var files = world.Service(Root);
        var drive = await world.DriveAsync();
        var images = await files.CreateFolderAsync(drive.Placement.Id, "Images", Token);
        await files.UploadAsync(Upload(images.Placement.Id, "chair.jpg"), Token);
        var hidden = await files.UploadAsync(Upload(images.Placement.Id, "chair.psd"), Token);
        world.Share(images.Placement.Id, ShareEntry.Grant(SharePrincipal.Role("Marketing"), AccessLevel.Manager));
        world.Share(hidden.Placement.Id, ShareEntry.Block(SharePrincipal.Role("Marketing")));

        var marketer = world.Service(User("mia", "Marketing"));
        Assert.Single(await marketer.ListChildrenAsync(images.Placement.Id, Token));

        var refusal = await Assert.ThrowsAsync<AccessDeniedException>(() => marketer.DeleteAsync(images.Placement.Id, Token));
        Assert.DoesNotContain("psd", refusal.Message);
        Assert.DoesNotContain("1", refusal.Message);
        Assert.True(world.Store.Placements.ContainsKey(images.Placement.Id));
        Assert.True(world.Store.Placements.ContainsKey(hidden.Placement.Id));
    }

    [Fact]
    public async Task MovingNeedsManagerOnWhatMovesAndEditorOnTheDestination()
    {
        var world = new FilesWorld();
        var files = world.Service(Root);
        var drive = await world.DriveAsync();
        var from = await files.CreateFolderAsync(drive.Placement.Id, "from", Token);
        var to = await files.CreateFolderAsync(drive.Placement.Id, "to", Token);
        var file = await files.UploadAsync(Upload(from.Placement.Id, "f.txt"), Token);
        world.Share(from.Placement.Id, ShareEntry.Grant(SharePrincipal.User("sam"), AccessLevel.Manager));
        world.Share(to.Placement.Id, ShareEntry.Grant(SharePrincipal.User("sam"), AccessLevel.Viewer));

        var sam = world.Service(User("sam"));
        await Assert.ThrowsAsync<AccessDeniedException>(() => sam.MoveAsync(file.Placement.Id, to.Placement.Id, Token));

        world.Share(to.Placement.Id, ShareEntry.Grant(SharePrincipal.User("sam"), AccessLevel.Editor));
        var moved = await sam.MoveAsync(file.Placement.Id, to.Placement.Id, Token);
        Assert.Equal(to.Placement.Id, moved.Placement.ParentId);
    }

    [Fact]
    public async Task WhatTheCallerCannotViewDoesNotExistForThem()
    {
        var world = new FilesWorld();
        var drive = await world.DriveAsync();
        var folder = await world.Service(Root).CreateFolderAsync(drive.Placement.Id, "f", Token);

        var stranger = world.Service(User("eve"));
        Assert.Null(await stranger.GetAsync(folder.Placement.Id, Token));
        Assert.Empty(await stranger.ListDrivesAsync(Token));
        await Assert.ThrowsAsync<MediaObjectNotFoundException>(() => stranger.DeleteAsync(folder.Placement.Id, Token));
        await Assert.ThrowsAsync<MediaObjectNotFoundException>(() => stranger.RenameAsync(folder.Placement.Id, "x", Token));
    }

    [Fact]
    public async Task AnEditorCreatesButCannotDelete()
    {
        var world = new FilesWorld();
        var drive = await world.DriveAsync();
        world.Share(drive.Placement.Id, ShareEntry.Grant(SharePrincipal.User("ed"), AccessLevel.Editor));

        var ed = world.Service(User("ed"));
        var folder = await ed.CreateFolderAsync(drive.Placement.Id, "new", Token);
        await ed.RenameAsync(folder.Placement.Id, "renamed", Token);

        await Assert.ThrowsAsync<AccessDeniedException>(() => ed.DeleteAsync(folder.Placement.Id, Token));
    }

    [Fact]
    public async Task APersonalDriveIsItsOwnersAndHiddenFromEveryoneElsesDriveList()
    {
        var world = new FilesWorld();
        var pat = world.Service(User("pat"));
        var drive = await pat.CreateDriveAsync("My drive", DriveType.Personal, MediaOwner.User("pat"), Token);
        var folder = await pat.CreateFolderAsync(drive.Placement.Id, "notes", Token);

        Assert.Equal(MediaOwner.User("pat"), folder.Content.Owner);
        Assert.Single(await pat.ListDrivesAsync(Token));
        Assert.Empty(await world.Service(Root).ListDrivesAsync(Token));
        await Assert.ThrowsAsync<MediaObjectConflictException>(() => pat.CreateDriveAsync("Again", DriveType.Personal, MediaOwner.User("pat"), Token));
    }

    [Fact]
    public async Task DriveTypesFollowTheTenantSettingsAndSharedDrivesNeedThePermission()
    {
        var world = new FilesWorld();
        world.Settings.PersonalDrives = false;

        await Assert.ThrowsAsync<MediaObjectConflictException>(() => world.Service(User("pat")).CreateDriveAsync("Mine", DriveType.Personal, MediaOwner.User("pat"), Token));
        await Assert.ThrowsAsync<AccessDeniedException>(() => world.Service(User("pat")).CreateDriveAsync("Team", DriveType.Shared, MediaOwner.Tenant, Token));

        var admin = new CallerContext
        {
            Tenant = "Default",
            Side = CallerSide.Admin,
            UserId = "drives",
            Permissions = new HashSet<string> { global::Crest.Media.MediaPermissions.ManageSharedDrives.Name },
        };
        var team = await world.Service(admin).CreateDriveAsync("Team", DriveType.Shared, MediaOwner.Tenant, Token);

        // Managing shared drives gives no access to their contents (requirement 31).
        Assert.Null(await world.Service(admin).GetAsync(team.Placement.Id, Token));
    }

    [Fact]
    public async Task AnInactiveDriveIsOutOfReachForAllButTenantAdministrators()
    {
        var world = new FilesWorld();
        var drive = await world.DriveAsync();
        var file = await world.Service(Root).UploadAsync(Upload(drive.Placement.Id, "f.txt"), Token);
        world.Share(drive.Placement.Id, ShareEntry.Grant(SharePrincipal.User("ann"), AccessLevel.Admin));
        world.Store.Contents[drive.Content.Id].Content.IsInactive = true;

        Assert.Null(await world.Service(User("ann")).GetAsync(file.Placement.Id, Token));
        Assert.NotNull(await world.Service(Root).GetAsync(file.Placement.Id, Token));
    }
}
