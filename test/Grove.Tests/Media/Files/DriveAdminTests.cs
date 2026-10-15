#nullable enable
using Grove.Access;
using Grove.Media.Files;
using Grove.Media.Sharing;
using Grove.Communications;
using Grove.Access.Sharing;
using Grove.Tests.Sharing;
using Grove.Access.Users;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using static Grove.Tests.Media.Files.FilesWorld;
using DriveType = Grove.Media.Files.DriveType;

namespace Grove.Tests.Media.Files;

/// <summary>Who holds Drive Admin: shared drives' admins are assigned by the tenant permission
/// (31), an organization's admins hold it on its drive (17), and on a personal drive nobody but
/// its owner does (19), so the § 7 access is the only way in.</summary>
public class DriveAdminTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static readonly CallerContext DriveManager = new()
    {
        Tenant = "Default",
        Side = CallerSide.Admin,
        UserId = "dm",
        UserName = "dm",
        Permissions = new HashSet<string> { global::Grove.Media.MediaPermissions.ManageSharedDrives.Name },
    };

    private static ShareableRef Ref(MediaItem item) => new(FileShareableKind.Name, item.Placement.Id);

    [Fact]
    public async Task TheSharedDrivesPermissionAssignsAdminsOnSharedDrivesOnly()
    {
        var world = new FilesWorld();
        var shared = await world.DriveAsync("Team");
        var personal = await world.Service(User("pat")).CreateDriveAsync("Pat", DriveType.Personal, MediaOwner.User("pat"), Token);
        var sharing = world.SharingService(DriveManager);

        await sharing.SetEntryAsync(Ref(shared), ShareEntry.Grant(SharePrincipal.User("dm"), AccessLevel.Admin), Token);

        // On a personal drive the permission assigns nothing: § 7 is the only way in.
        await Assert.ThrowsAsync<AccessDeniedException>(() => sharing.SetEntryAsync(Ref(personal), ShareEntry.Grant(SharePrincipal.User("dm"), AccessLevel.Admin), Token));
        Assert.Equal(AccessLevel.None, (await world.Resolver().ResolveAsync(DriveManager, Ref(personal), Token)).Level);
    }

    [Fact]
    public async Task OnAPersonalDriveNobodyButItsOwnerIsGrantedAdmin()
    {
        var world = new FilesWorld();
        var personal = await world.Service(User("pat")).CreateDriveAsync("Pat", DriveType.Personal, MediaOwner.User("pat"), Token);

        // Not by its owner, not by a tenant administrator.
        await Assert.ThrowsAsync<ArgumentException>(() => world.SharingService(User("pat")).SetEntryAsync(Ref(personal), ShareEntry.Grant(SharePrincipal.User("sam"), AccessLevel.Admin), Token));
        await Assert.ThrowsAsync<ArgumentException>(() => world.SharingService(Root).SetEntryAsync(Ref(personal), ShareEntry.Grant(SharePrincipal.Role("Staff"), AccessLevel.Admin), Token));

        // Sharing below Admin is the owner's to give.
        await world.SharingService(User("pat")).SetEntryAsync(Ref(personal), ShareEntry.Grant(SharePrincipal.User("sam"), AccessLevel.Manager), Token);

        // A stored Admin grant from anywhere else counts as Manager at most.
        world.Share(personal.Placement.Id, ShareEntry.Grant(SharePrincipal.User("eve"), AccessLevel.Admin));
        Assert.Equal(AccessLevel.Manager, (await world.Resolver().ResolveAsync(User("eve"), Ref(personal), Token)).Level);
        Assert.Equal(AccessLevel.Admin, (await world.Resolver().ResolveAsync(User("pat"), Ref(personal), Token)).Level);
    }

    [Fact]
    public void TheKindAssignsOnSharedRootsAndLimitsPersonalOnes()
    {
        var kind = new FilesWorld().Kind;
        ShareableObject Root(string type, string? owner = null) => new()
        {
            Ref = new ShareableRef(MediaFileShareableKind.KindName, "d"),
            OwnerUserId = owner,
            Facts = new Dictionary<string, IReadOnlyList<string>> { [MediaFileShareableKind.Facts.DriveType] = [type] },
        };

        Assert.True(kind.AppliesTo(Root("shared")));
        Assert.False(kind.AppliesTo(Root("organization")));
        Assert.False(kind.AppliesTo(Root("personal", "pat")));
        Assert.True(kind.MayHoldAdmin(Root("personal", "pat"), SharePrincipal.User("pat")));
        Assert.False(kind.MayHoldAdmin(Root("personal", "pat"), SharePrincipal.User("sam")));
        Assert.True(kind.MayHoldAdmin(Root("shared"), SharePrincipal.Role("Staff")));
    }

    [Fact]
    public async Task AnOrganizationsAdminsAreItsDrivesAdminsAndNoOtherOrganizations()
    {
        var world = new FilesWorld { WithSharingService = true };
        var drive = await world.Service(Root).CreateDriveAsync("Acme", DriveType.Organization, MediaOwner.Organization("acme"), Token);
        var role = world.Settings.OrganizationAdminRole;

        var entry = Assert.Single(world.Sharing.Store.Documents[Ref(drive)].Entries);
        Assert.Equal(ShareEntry.Grant(SharePrincipal.Role(role, "acme"), AccessLevel.Admin), entry);

        var resolver = world.Resolver();
        Assert.Equal(AccessLevel.Admin, (await resolver.ResolveAsync(SharingWorld.Member("ann", "acme", role), Ref(drive), Token)).Level);
        Assert.Equal(AccessLevel.None, (await resolver.ResolveAsync(SharingWorld.Member("bob", "globex", role), Ref(drive), Token)).Level);
        Assert.Equal(AccessLevel.None, (await resolver.ResolveAsync(SharingWorld.Member("cy", "acme", "Member"), Ref(drive), Token)).Level);
    }

    [Fact]
    public async Task ANoticeByBothChannelsGoesOutOnceOnEach()
    {
        var owner = new Mock<IUser>().Object;
        var users = new Mock<UserManager<IUser>>(Mock.Of<IUserStore<IUser>>(), null!, null!, null!, null!, null!, null!, null!, null!);
        users.Setup(manager => manager.FindByIdAsync("pat")).ReturnsAsync(owner);
        // Portal notifications on (the feature registers the marker), so a portal notice is stored.
        var services = new ServiceCollection().AddSingleton(users.Object).AddSingleton(Mock.Of<IPortalNotifications>()).BuildServiceProvider();

        var session = new Mock<YesSql.ISession>();
        var notifications = new Mock<INotificationService>(MockBehavior.Strict);
        var email = new Mock<INotificationMethodProvider>();
        email.SetupGet(method => method.Method).Returns("Email");
        email.Setup(method => method.SendAsync(owner, It.IsAny<INotificationMessage>(), It.IsAny<CancellationToken>())).ReturnsAsync(global::Grove.Infrastructure.Result.Success());

        var notifier = new NotificationPersonalDriveNotifier(services, session.Object, new SharingWorld.TestClock(), [notifications.Object], [], [email.Object]);
        var (sent, detail) = await notifier.NotifyAsync("pat", PersonalDriveNotice.Email | PersonalDriveNotice.Portal, "subject", "body", Token);

        Assert.True(sent, detail);

        // Stored once for the portal, never sent through the user's own methods; emailed once.
        session.Verify(store => store.SaveAsync(It.Is<object>(item => item is Notification), It.IsAny<bool>(), NotificationConstants.NotificationCollection, It.IsAny<CancellationToken>()), Times.Once);
        notifications.Verify(service => service.SendAsync(It.IsAny<object>(), It.IsAny<INotificationMessage>(), It.IsAny<CancellationToken>()), Times.Never);
        email.Verify(method => method.SendAsync(owner, It.IsAny<INotificationMessage>(), It.IsAny<CancellationToken>()), Times.Once);
    }
}
