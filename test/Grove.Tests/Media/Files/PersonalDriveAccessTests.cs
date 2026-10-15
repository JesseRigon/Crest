#nullable enable
using Grove.Access;
using Grove.Media.Files;
using static Grove.Tests.Media.Files.FilesWorld;
using DriveType = Grove.Media.Files.DriveType;

namespace Grove.Tests.Media.Files;

/// <summary>Administrator access to personal drives (docs/media.md § 7, requirements 24-29):
/// a separate, deliberate, time-bound step with notice, delay, override and logging.</summary>
public class PersonalDriveAccessTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static async Task<(FilesWorld World, MediaItem Drive, MediaItem File)> PatsDriveAsync()
    {
        var world = new FilesWorld();
        var pat = world.Service(User("pat"));
        var drive = await pat.CreateDriveAsync("Pat's drive", DriveType.Personal, MediaOwner.User("pat"), Token);
        var file = await pat.UploadAsync(Upload(drive.Placement.Id, "diary.txt"), Token);
        return (world, drive, file);
    }

    [Fact]
    public async Task WithoutAnAccessAnotherUsersDriveDoesNotExistForATenantAdministrator()
    {
        var (world, drive, file) = await PatsDriveAsync();
        var root = world.Service(Root);

        Assert.Empty(await root.ListDrivesAsync(Token));
        Assert.Null(await root.GetAsync(file.Placement.Id, Token));
        Assert.Null(await root.OpenReadAsync(file.Placement.Id, cancellationToken: Token));
        await Assert.ThrowsAsync<MediaObjectNotFoundException>(() => root.ListChildrenAsync(drive.Placement.Id, Token));

        // The separate step finds the drive itself, not what is in it.
        Assert.Equal(drive.Placement.Id, (await root.FindPersonalDriveAsync("pat", Token))!.Placement.Id);
    }

    [Fact]
    public async Task AnAccessOpensTheDriveAndEveryActionIsRecordedAsAdministratorAccess()
    {
        var (world, drive, file) = await PatsDriveAsync();

        var access = await world.PersonalAccess(Root).RequestAsync(drive.Placement.Id, TimeSpan.FromMinutes(30), false, null, Token);
        var root = world.Service(Root);

        Assert.NotNull(await root.GetAsync(file.Placement.Id, Token));
        Assert.Contains(world.Audit.Events, e => e.Operation == PersonalDriveAccessService.RequestOperation);
        Assert.Contains(world.Audit.Events, e => e.Operation == PersonalDriveAccessService.OpenOperation);
        Assert.Contains(world.Audit.Events, e => e.Reason == $"{PersonalDriveAccessService.AdministratorAccessReason} {access.Id}" && e.Resource == $"file:{file.Placement.Id}");

        // Still never in the everyday drive list (requirement 24).
        Assert.Empty(await root.ListDrivesAsync(Token));
    }

    [Fact]
    public async Task TheDelayIsEnforcedAfterTheNotice()
    {
        var (world, drive, file) = await PatsDriveAsync();
        world.PersonalDriveAccess.Notice = PersonalDriveNotice.Email | PersonalDriveNotice.Portal;
        world.PersonalDriveAccess.Delay = TimeSpan.FromMinutes(30);

        var access = await world.PersonalAccess(Root).RequestAsync(drive.Placement.Id, TimeSpan.FromHours(1), false, null, Token);

        Assert.Equal("pat", Assert.Single(world.Notifier.Sent).Owner);
        Assert.Equal(access.RequestedUtc + TimeSpan.FromMinutes(30), access.OpensUtc);
        Assert.Null(await world.Service(Root).GetAsync(file.Placement.Id, Token));

        world.Sharing.Clock.UtcNow = world.Sharing.Clock.UtcNow.AddMinutes(31);
        Assert.NotNull(await world.Service(Root).GetAsync(file.Placement.Id, Token));
    }

    [Fact]
    public async Task ADelayWithoutANoticeDoesNotApply()
    {
        var (world, drive, file) = await PatsDriveAsync();
        world.PersonalDriveAccess.Delay = TimeSpan.FromMinutes(30);

        await world.PersonalAccess(Root).RequestAsync(drive.Placement.Id, null, false, null, Token);

        Assert.NotNull(await world.Service(Root).GetAsync(file.Placement.Id, Token));
    }

    [Fact]
    public async Task OverridingTheDelayNeedsTheSettingAndAReasonWhichTheNoticeCarries()
    {
        var (world, drive, file) = await PatsDriveAsync();
        world.PersonalDriveAccess.Notice = PersonalDriveNotice.Email;
        world.PersonalDriveAccess.Delay = TimeSpan.FromMinutes(30);
        var admin = world.PersonalAccess(Root);

        await Assert.ThrowsAsync<MediaObjectConflictException>(() => admin.RequestAsync(drive.Placement.Id, null, true, "legal hold", Token));

        world.PersonalDriveAccess.AllowDelayOverride = true;
        await Assert.ThrowsAsync<ArgumentException>(() => admin.RequestAsync(drive.Placement.Id, null, true, " ", Token));

        var access = await admin.RequestAsync(drive.Placement.Id, null, true, "legal hold", Token);
        Assert.True(access.Overridden);
        Assert.Equal(access.RequestedUtc, access.OpensUtc);
        Assert.Contains("legal hold", world.Notifier.Sent.Last().Body);
        Assert.Contains(world.Audit.Events, e => e.Operation == PersonalDriveAccessService.OverrideOperation && e.Reason == "legal hold");
        Assert.NotNull(await world.Service(Root).GetAsync(file.Placement.Id, Token));
    }

    [Fact]
    public async Task TheDurationComesFromTheTenantsListAndTheAccessEndsOnTime()
    {
        var (world, drive, file) = await PatsDriveAsync();
        var admin = world.PersonalAccess(Root);

        await Assert.ThrowsAsync<ArgumentException>(() => admin.RequestAsync(drive.Placement.Id, TimeSpan.FromMinutes(7), false, null, Token));

        await admin.RequestAsync(drive.Placement.Id, TimeSpan.FromMinutes(30), false, null, Token);
        Assert.NotNull(await world.Service(Root).GetAsync(file.Placement.Id, Token));

        world.Sharing.Clock.UtcNow = world.Sharing.Clock.UtcNow.AddMinutes(30);
        Assert.Null(await world.Service(Root).GetAsync(file.Placement.Id, Token));
        Assert.Contains(world.Audit.Events, e => e.Operation == PersonalDriveAccessService.EndOperation);
    }

    [Fact]
    public async Task AFailedNoticeIsRecordedAndTheAccessProceeds()
    {
        var (world, drive, file) = await PatsDriveAsync();
        world.PersonalDriveAccess.Notice = PersonalDriveNotice.Email;
        world.Notifier.Fails = true;

        var access = await world.PersonalAccess(Root).RequestAsync(drive.Placement.Id, null, false, null, Token);

        Assert.Contains("failed", world.Accesses.Documents[access.Id].Notice);
        Assert.Contains(world.Audit.Events, e => e.Operation == PersonalDriveAccessService.NoticeOperation && e.Verdict == AccessVerdict.Deny);
        Assert.NotNull(await world.Service(Root).GetAsync(file.Placement.Id, Token));
    }

    [Fact]
    public async Task ClosingEndsTheAccess()
    {
        var (world, drive, file) = await PatsDriveAsync();
        var admin = world.PersonalAccess(Root);
        var access = await admin.RequestAsync(drive.Placement.Id, null, false, null, Token);

        await admin.CloseAsync(access.Id, Token);

        Assert.Null(await world.Service(Root).GetAsync(file.Placement.Id, Token));
        Assert.Contains(world.Audit.Events, e => e.Operation == PersonalDriveAccessService.CloseOperation);
    }

    [Fact]
    public async Task OnlyATenantAdministratorAsksAndNeverForTheirOwnDrive()
    {
        var (world, drive, _) = await PatsDriveAsync();

        await Assert.ThrowsAsync<AccessDeniedException>(() => world.PersonalAccess(User("sam")).RequestAsync(drive.Placement.Id, null, false, null, Token));

        var own = new CallerContext { Tenant = "Default", Side = CallerSide.Admin, UserId = "pat", IsSuperUser = true };
        await Assert.ThrowsAsync<MediaObjectNotFoundException>(() => world.PersonalAccess(own).RequestAsync(drive.Placement.Id, null, false, null, Token));
    }
}
