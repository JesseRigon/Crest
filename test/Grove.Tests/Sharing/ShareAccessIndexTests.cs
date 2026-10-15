using Grove.Access;
using Grove.Access.Sharing;
using Grove.Access.Sharing.Services;
using Grove.Tests.Access.Conformance;
using static Grove.Tests.Sharing.SharingWorld;

namespace Grove.Tests.Sharing;

/// <summary>
/// The access index (docs/sharing.md, requirements 75-81): its rows for the example drive, its
/// windows over time and the time registry, recalculation on change (removals before the call
/// returns, large additions in the background) and verification against the resolver.
/// </summary>
public class ShareAccessIndexTests
{
    private static string Row(ShareAccessRow row) =>
        $"{row.PrincipalKind}:{row.PrincipalId}@{row.Depth}={(row.IsBlock ? "block" : row.Level.ToString())}";

    private static async Task<string[]> RowsAsync(SharingWorld world, string id) =>
        [.. (await world.AccessStore.GetAsync(Ref(id)))!.Rows.Select(Row).Order()];

    [Fact]
    public async Task TheExampleDriveIsIndexedPerPrincipalWithTheWinningDecision()
    {
        var world = Example();
        await world.IndexAllAsync();

        Assert.Equal(["Role:Customer@1=Viewer", "Role:Staff@1=Viewer", "User:sam@1=block"], await RowsAsync(world, "product-info"));
        Assert.Equal(["Role:Customer@1=Viewer", "Role:Staff@1=Viewer", "Tenant:@2=Viewer", "User:sam@1=block"], await RowsAsync(world, "spec-sheets"));

        // Rule 1 lives on Images: a Photoshop file in Spec sheets is untouched.
        Assert.Equal(await RowsAsync(world, "spec-sheets"), await RowsAsync(world, "spec.psd"));
        Assert.Equal(["Role:Customer@1=Viewer", "Role:Marketing@2=Editor", "Role:Staff@1=Viewer", "User:sam@1=block"], await RowsAsync(world, "chair-original.jpg"));

        // chair.psd: the Customer grant is blocked deeper by rule 1, and Dana edits.
        Assert.Equal(["Role:Customer@2=block", "Role:Marketing@2=Editor", "Role:Staff@1=Viewer", "User:dana@2=Editor", "User:sam@1=block"], await RowsAsync(world, "chair.psd"));

        // The Customer block outranks role grants shallower than it (Staff at depth 1), not
        // those at its own depth (Marketing at 2: the grant wins).
        var psd = (await world.AccessStore.GetAsync(Ref("chair.psd")))!;
        var shadow = Assert.Single(psd.Shadows);
        Assert.Equal(("Customer", 1), (shadow.Role, shadow.Depth));

        // Rule 2 makes final images public.
        var final = (await world.AccessStore.GetAsync(Ref("chair-final.jpg")))!;
        Assert.True(final.IsPublic);
        Assert.Contains("Anyone:@2=Viewer", final.Rows.Select(Row));
        Assert.False((await world.AccessStore.GetAsync(Ref("chair.psd")))!.IsPublic);

        foreach (var id in world.Files.Nodes.Keys)
        {
            Assert.Empty(await world.Indexer().VerifyAsync(Ref(id), TestContext.Current.CancellationToken));
        }
    }

    [Fact]
    public async Task OwnersAndInactiveObjectsAndBrokenChains()
    {
        var world = new SharingWorld();
        world.Files.Add("drive", owner: "olga");
        world.Files.Add("folder", "drive", owner: "otto");
        world.Files.Add("old", inactive: true);
        world.Files.Add("orphan", "missing");
        world.Share("folder", ShareEntry.Block(SharePrincipal.User("otto")));
        await world.IndexAllAsync();

        // The owner holds Manager whatever the user tier says; the root's owner administers the tree.
        var folder = (await world.AccessStore.GetAsync(Ref("folder")))!;
        Assert.Contains(folder.Rows, row => row is { PrincipalId: "otto", IsOwner: true, Level: AccessLevel.Manager, IsBlock: false });
        Assert.Contains(folder.Rows, row => row is { PrincipalId: "olga", IsOwner: true, Level: AccessLevel.Admin });
        Assert.DoesNotContain(folder.Rows, row => row.IsBlock);

        // Inactive: indexed with no rows. A chain that does not resolve: not indexed (82).
        var old = (await world.AccessStore.GetAsync(Ref("old")))!;
        Assert.True(old.IsInactive);
        Assert.Empty(old.Rows);
        Assert.Null(await world.AccessStore.GetAsync(Ref("orphan")));
    }

    [Fact]
    public async Task RowsCarryTheirWindowsSoAnEndedBlockFallsBackToTheShallowerDecision()
    {
        var world = Example();
        var now = new DateTimeOffset(world.Clock.UtcNow, TimeSpan.Zero);
        var ends = now.AddHours(1);
        world.Share("product-info", ShareEntry.Grant(SharePrincipal.User("eve"), AccessLevel.Viewer));
        world.Share("images", ShareEntry.Block(SharePrincipal.User("eve"), ends: ends));
        world.Share("spec-sheets", ShareEntry.Grant(SharePrincipal.User("tia"), AccessLevel.Viewer, starts: now.AddMinutes(30), ends: ends));
        await world.IndexAllAsync();

        var images = (await world.AccessStore.GetAsync(Ref("images")))!;
        var eve = images.Rows.Where(row => row.PrincipalId == "eve").OrderBy(row => row.StartsUtc ?? DateTime.MinValue).ToList();
        Assert.Equal(2, eve.Count);
        Assert.True(eve[0] is { IsBlock: true, Depth: 2, StartsUtc: null });
        Assert.Equal(ends.UtcDateTime, eve[0].EndsUtc);
        Assert.True(eve[1] is { IsBlock: false, Depth: 1, EndsUtc: null });
        Assert.Equal(ends.UtcDateTime, eve[1].StartsUtc);

        // The time registry: the next boundary on the chain.
        Assert.Equal(ends.UtcDateTime, images.NextBoundaryUtc);
        var spec = (await world.AccessStore.GetAsync(Ref("spec-sheets")))!;
        Assert.Equal(now.AddMinutes(30).UtcDateTime, spec.NextBoundaryUtc);
        var tia = Assert.Single(spec.Rows, row => row.PrincipalId == "tia");
        Assert.Equal((now.AddMinutes(30).UtcDateTime, ends.UtcDateTime), (tia.StartsUtc!.Value, tia.EndsUtc!.Value));

        // The job reads only what is due, recalculates it and registers the next boundary;
        // passed windows are dropped.
        // Spec sheets and spec.psd below it are due; Images' subtree is not yet.
        world.Clock.UtcNow = now.AddMinutes(31).UtcDateTime;
        Assert.Equal(2, await world.Indexer().RecalculateDueAsync(100, TestContext.Current.CancellationToken));
        Assert.Equal(ends.UtcDateTime, (await world.AccessStore.GetAsync(Ref("spec-sheets")))!.NextBoundaryUtc);

        world.Clock.UtcNow = now.AddHours(2).UtcDateTime;
        Assert.True(await world.Indexer().RecalculateDueAsync(100, TestContext.Current.CancellationToken) > 0);
        images = (await world.AccessStore.GetAsync(Ref("images")))!;
        Assert.Equal("User:eve@1=Viewer", Row(Assert.Single(images.Rows, row => row.PrincipalId == "eve")));
        Assert.Null(images.NextBoundaryUtc);
        Assert.DoesNotContain((await world.AccessStore.GetAsync(Ref("spec-sheets")))!.Rows, row => row.PrincipalId == "tia");
    }

    private sealed class Accessor(CallerContext current) : ICallerContextAccessor
    {
        public CallerContext Current { get; set; } = current;
    }

    private sealed class Decision : IAccessDecision
    {
        public Task<AccessDecision> DecideAsync(CallerContext caller, string permission, object resource = null, CancellationToken cancellationToken = default) =>
            Task.FromResult(caller.IsSuperUser || caller.Permissions.Contains(permission) ? AccessDecision.Allowed : AccessDecision.Denied($"'{permission}' is not granted."));
    }

    private static SharingService Service(SharingWorld world, CallerContext caller) =>
        new(new Accessor(caller), new Decision(), world.Resolver(), world.Store, world.Kinds(), new AccessWorld.RecordingAuditor(), world.Indexer(), world.AccessStore, world.Clock);

    private static CallerContext DriveAdmin(SharingWorld world)
    {
        world.Share("product-info", ShareEntry.Grant(SharePrincipal.User("da"), AccessLevel.Admin));
        return User("da");
    }

    [Fact]
    public async Task RemovingAGrantIsIndexedBeforeTheCallReturns()
    {
        var world = Example();
        var service = Service(world, DriveAdmin(world));
        await world.IndexAllAsync();

        await service.RemoveEntryAsync(Ref("images"), SharePrincipal.Role("Marketing"), TestContext.Current.CancellationToken);

        foreach (var id in new[] { "images", "chair-original.jpg", "chair.psd", "chair-final.jpg" })
        {
            Assert.DoesNotContain(await RowsAsync(world, id), row => row.StartsWith("Role:Marketing", StringComparison.Ordinal));
            Assert.Empty(await world.Indexer().VerifyAsync(Ref(id), TestContext.Current.CancellationToken));
        }

        Assert.Empty(world.AccessStore.Queue);
    }

    [Fact]
    public async Task ChangingRulesRecalculatesTheSubtree()
    {
        var world = Example();

        // The rules written back keep rule 2's Anyone grant, which takes ShareWithAnyone.
        var admin = DriveAdmin(world);
        var service = Service(world, new CallerContext { Tenant = admin.Tenant, Side = admin.Side, UserId = admin.UserId, UserName = admin.UserName, Permissions = new HashSet<string> { "ShareWithAnyone" } });
        await world.IndexAllAsync();

        // Without rule 1, customers see chair.psd again and Dana loses it.
        var rules = await service.GetRulesAsync(Ref("images"), TestContext.Current.CancellationToken);
        await service.SetRulesAsync(Ref("images"), [.. rules.Where(rule => rule.Id != "rule-1")], TestContext.Current.CancellationToken);

        Assert.Equal(["Role:Customer@1=Viewer", "Role:Marketing@2=Editor", "Role:Staff@1=Viewer", "User:da@1=Admin", "User:sam@1=block"], await RowsAsync(world, "chair.psd"));
        Assert.Empty((await world.AccessStore.GetAsync(Ref("chair.psd")))!.Shadows);
    }

    [Fact]
    public async Task AddingAccessToALargeSubtreeIsQueuedAndRemovingItIsNot()
    {
        var world = Example();
        var service = Service(world, DriveAdmin(world));
        world.Files.Add("big", "product-info");
        for (var i = 0; i < ShareAccessIndexer.SynchronousLimit + 10; i++)
        {
            world.Files.Add($"big-{i}", "big");
        }

        await world.IndexAllAsync();

        // Adding: more objects than the limit, so the subtree waits for the job (shown pending).
        await service.SetEntryAsync(Ref("big"), ShareEntry.Grant(SharePrincipal.User("dana"), AccessLevel.Viewer), TestContext.Current.CancellationToken);
        Assert.Equal(Ref("big"), Assert.Single(world.AccessStore.Queue).Ref);
        Assert.DoesNotContain(await RowsAsync(world, "big-0"), row => row.StartsWith("User:dana", StringComparison.Ordinal));

        Assert.Equal(1, await world.Indexer().RecalculateQueuedAsync(10, TestContext.Current.CancellationToken));
        Assert.Empty(world.AccessStore.Queue);
        Assert.Contains("User:dana@2=Viewer", await RowsAsync(world, "big-0"));

        // Removing: applied to every object before the call returns, however many.
        await service.RemoveEntryAsync(Ref("big"), SharePrincipal.User("dana"), TestContext.Current.CancellationToken);
        Assert.Empty(world.AccessStore.Queue);
        foreach (var i in new[] { 0, ShareAccessIndexer.SynchronousLimit + 9 })
        {
            Assert.DoesNotContain(await RowsAsync(world, $"big-{i}"), row => row.StartsWith("User:dana", StringComparison.Ordinal));
        }
    }

    [Theory]
    [InlineData(null, true)]
    [InlineData("block", true)]
    [InlineData("grant", true)]
    [InlineData("grant-bounded", true)]
    public void AGrantThatContainsWhatItReplacesWidens(string previous, bool widens)
    {
        var principal = SharePrincipal.User("u");
        var now = DateTimeOffset.UtcNow;
        var old = previous switch
        {
            "block" => ShareEntry.Block(principal),
            "grant" => ShareEntry.Grant(principal, AccessLevel.Editor),
            "grant-bounded" => ShareEntry.Grant(principal, AccessLevel.Viewer, now, now.AddDays(1)),
            _ => null,
        };

        Assert.Equal(widens, SharingService.Widens(old, ShareEntry.Grant(principal, AccessLevel.Viewer)));
    }

    [Fact]
    public void ABlockOrAShorterGrantNarrows()
    {
        var principal = SharePrincipal.User("u");
        var now = DateTimeOffset.UtcNow;

        Assert.False(SharingService.Widens(null, ShareEntry.Block(principal)));
        Assert.False(SharingService.Widens(ShareEntry.Grant(principal, AccessLevel.Viewer), ShareEntry.Grant(principal, AccessLevel.Viewer, ends: now.AddDays(1))));
        Assert.False(SharingService.Widens(ShareEntry.Grant(principal, AccessLevel.Viewer), ShareEntry.Grant(principal, AccessLevel.Viewer, starts: now.AddDays(1))));
    }

    [Fact]
    public async Task SharedWithMeReadsTheIndexIncludingContributedEntries()
    {
        var world = Example();
        var contributed = new ContributingSource();
        contributed.Entries[Ref("spec.psd")] = [ShareEntry.Grant(SharePrincipal.User("dana"), AccessLevel.Viewer)];
        var resolver = new SharingResolver(world.Kinds(), world.Store, world.Clock, [contributed]);
        var indexer = new ShareAccessIndexer(world.Kinds(), resolver, world.AccessStore, world.Clock);
        foreach (var id in world.Files.Nodes.Keys)
        {
            await indexer.RecalculateAsync(Ref(id), cancellationToken: TestContext.Current.CancellationToken);
        }

        var service = new SharingService(new Accessor(User("dana")), new Decision(), resolver, world.Store, world.Kinds(), new AccessWorld.RecordingAuditor(), indexer, world.AccessStore, world.Clock);

        // The contributed grant on spec.psd counts; the rule grant on chair.psd is not a share
        // to Dana on that object, and inherited grants are not listed object by object.
        Assert.Equal([Ref("spec.psd")], await service.SharedWithMeAsync(TestContext.Current.CancellationToken));
    }

    private sealed class ContributingSource : IShareEntrySource
    {
        public Dictionary<ShareableRef, ShareEntry[]> Entries { get; } = [];

        public string Name => "test";

        public Task<ShareContribution> GetAsync(ShareableRef target, CancellationToken cancellationToken = default) =>
            Task.FromResult(Entries.TryGetValue(target, out var entries) ? new ShareContribution(entries, []) : null);
    }
}
