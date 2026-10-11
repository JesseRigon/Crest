#nullable enable
using Crest.Access;
using Crest.Media.Files;
using static Crest.Tests.Media.Files.FilesWorld;
using DriveType = Crest.Media.Files.DriveType;

namespace Crest.Tests.Media.Files;

/// <summary>Inactive drives by setting (requirement 21), reactivation (22) and what happens to a
/// deleted user's personal drive (20).</summary>
public class DriveAdministrationTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static async Task<(FilesWorld World, MediaItem Drive, MediaItem Folder, MediaItem File)> PatsDriveAsync()
    {
        var world = new FilesWorld();
        var pat = world.Service(User("pat"));
        var drive = await pat.CreateDriveAsync("Pat", DriveType.Personal, MediaOwner.User("pat"), Token);
        var folder = await pat.CreateFolderAsync(drive.Placement.Id, "notes", Token);
        var file = await pat.UploadAsync(Upload(folder.Placement.Id, "a.txt"), Token);
        return (world, drive, folder, file);
    }

    [Fact]
    public async Task TurningADriveTypeOffMakesItsDrivesInactiveAndOnAgainActive()
    {
        var (world, drive, _, file) = await PatsDriveAsync();
        var root = world.Service(Root);

        Assert.Equal(1, await root.SetDriveTypeEnabledAsync(DriveType.Personal, enabled: false, cancellationToken: Token));
        Assert.True(world.Store.Contents[drive.Content.Id].Content.IsInactive);
        Assert.Null(await world.Service(User("pat")).GetAsync(file.Placement.Id, Token));

        Assert.Equal(1, await root.SetDriveTypeEnabledAsync(DriveType.Personal, enabled: true, cancellationToken: Token));
        Assert.NotNull(await world.Service(User("pat")).GetAsync(file.Placement.Id, Token));
    }

    [Fact]
    public async Task OnlyATenantAdministratorChangesDrives()
    {
        var (world, drive, _, _) = await PatsDriveAsync();

        await Assert.ThrowsAsync<AccessDeniedException>(() => world.Service(User("pat")).SetDriveInactiveAsync(drive.Placement.Id, true, cancellationToken: Token));
        await Assert.ThrowsAsync<AccessDeniedException>(() => world.Service(User("pat")).DisposePersonalDriveAsync("pat", PersonalDriveDisposal.Delete, cancellationToken: Token));
    }

    [Fact]
    public void AllFourChoicesAreOfferedUntilRetentionDecides() =>
        Assert.Equal(4, new FilesWorld().Service(Root).DisposalChoices.Count);

    [Fact]
    public async Task KeepingADeletedUsersDriveMakesItInactive()
    {
        var (world, drive, _, _) = await PatsDriveAsync();

        await world.Service(Root).DisposePersonalDriveAsync("pat", PersonalDriveDisposal.KeepInactive, cancellationToken: Token);

        Assert.True(world.Store.Contents[drive.Content.Id].Content.IsInactive);
    }

    [Fact]
    public async Task DeletingADeletedUsersDriveRemovesEverything()
    {
        var (world, _, _, _) = await PatsDriveAsync();

        await world.Service(Root).DisposePersonalDriveAsync("pat", PersonalDriveDisposal.Delete, cancellationToken: Token);

        Assert.Empty(world.Store.Placements);
        Assert.Empty(world.Store.Contents);
        Assert.Empty(world.Storage.Blobs);
    }

    [Fact]
    public async Task TransferringToAUserWithoutADriveMakesItTheirs()
    {
        var (world, drive, folder, file) = await PatsDriveAsync();

        await world.Service(Root).DisposePersonalDriveAsync("pat", PersonalDriveDisposal.TransferToUser, "quinn", Token);

        Assert.Equal(MediaOwner.User("quinn"), world.Store.Contents[drive.Content.Id].Content.Owner);
        Assert.Equal(MediaOwner.User("quinn"), world.Store.Contents[file.Content.Id].Content.Owner);
        Assert.NotNull(await world.Service(User("quinn")).GetAsync(file.Placement.Id, Token));
        Assert.Null(await world.Service(User("pat")).GetAsync(folder.Placement.Id, Token));
    }

    [Fact]
    public async Task TransferringToAUserWithADriveMovesEverythingIntoAFolderOfIt()
    {
        var (world, drive, folder, file) = await PatsDriveAsync();
        var theirs = await world.Service(User("quinn")).CreateDriveAsync("Quinn", DriveType.Personal, MediaOwner.User("quinn"), Token);

        await world.Service(Root).DisposePersonalDriveAsync("pat", PersonalDriveDisposal.TransferToUser, "quinn", Token);

        Assert.False(world.Store.Placements.ContainsKey(drive.Placement.Id));
        var moved = world.Store.Placements[folder.Placement.Id];
        Assert.Equal(theirs.Placement.Id, moved.Placement.DriveId);
        Assert.Contains(theirs.Placement.Id, await world.Graph().AncestorsAsync(file.Placement.Id, Token));
        Assert.Equal(MediaOwner.User("quinn"), world.Store.Contents[file.Content.Id].Content.Owner);
    }

    [Fact]
    public async Task MovingIntoASharedDriveHandsEverythingToTheTenant()
    {
        var (world, drive, folder, file) = await PatsDriveAsync();
        var shared = await world.DriveAsync("Archive");

        await world.Service(Root).DisposePersonalDriveAsync("pat", PersonalDriveDisposal.MoveToSharedDrive, shared.Placement.Id, Token);

        Assert.False(world.Store.Contents.ContainsKey(drive.Content.Id));
        Assert.Equal(shared.Placement.Id, world.Store.Placements[folder.Placement.Id].Placement.DriveId);
        Assert.Equal(MediaOwner.Tenant, world.Store.Contents[file.Content.Id].Content.Owner);
    }
}
