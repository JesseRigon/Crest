using Crest.Access;
using Crest.Sharing;
using Crest.Sharing.Services;
using static Crest.Tests.Sharing.SharingWorld;

namespace Crest.Tests.Sharing;

/// <summary>
/// Adding sharing without becoming the authority (docs/sharing.md › Extending or replacing
/// sharing): a source's entries and rules are resolved with the stored ones under the same
/// precedence, so a contribution wins only where sharing's own rules let it.
/// </summary>
public class ShareEntrySourceTests
{
    private sealed class Source(string name, Dictionary<string, ShareContribution> byObject) : IShareEntrySource
    {
        public string Name => name;

        public Task<ShareContribution> GetAsync(ShareableRef target, CancellationToken cancellationToken = default) =>
            Task.FromResult(byObject.GetValueOrDefault(target.Id));
    }

    private sealed class LowSource(string name, Dictionary<string, ShareContribution> byObject) : IShareEntrySource
    {
        public string Name => name;

        public Task<ShareContribution> GetAsync(ShareableRef target, CancellationToken cancellationToken = default) =>
            Task.FromResult(byObject.GetValueOrDefault(target.Id));
    }

    private static SharingResolver Resolver(SharingWorld world, params IShareEntrySource[] sources) =>
        new(world.Kinds(), world.Store, world.Clock, sources);

    private static ShareContribution Entries(params ShareEntry[] entries) => new(entries, []);

    [Fact]
    public async Task AContributedGrantCountsWhereNothingElseDecides()
    {
        // A CRM says Dana is the account's contact: she may view the drive.
        var world = Example();
        var crm = new Source("crm", new() { ["product-info"] = Entries(ShareEntry.Grant(SharePrincipal.User("dana"), AccessLevel.Viewer)) });

        var resolution = await Resolver(world, crm).ResolveAsync(User("dana"), Ref("chair-original.jpg"), TestContext.Current.CancellationToken);

        Assert.Equal(AccessLevel.Viewer, resolution.Level);
        Assert.Contains(resolution.Decisions, d => d.Source == "crm");
    }

    [Fact]
    public async Task AContributedGrantCannotBeatAMoreSpecificPrincipalsBlock()
    {
        // Sam's stored user block outranks a contributed Staff (role) grant at any depth.
        var world = Example();
        var source = new Source("hr", new() { ["chair-original.jpg"] = Entries(ShareEntry.Grant(SharePrincipal.Role("Staff"), AccessLevel.Editor)) });

        Assert.Equal(AccessLevel.None, (await Resolver(world, source).ResolveAsync(User("sam", "Staff"), Ref("chair-original.jpg"), TestContext.Current.CancellationToken)).Level);
    }

    [Fact]
    public async Task AContributedGrantCannotBeatADeeperStoredBlock()
    {
        // The contribution grants Customer on the drive; rule 1's block on Photoshop files under
        // Images is deeper and stands.
        var world = Example();
        var source = new Source("partner", new() { ["product-info"] = Entries(ShareEntry.Grant(SharePrincipal.Role("Customer"), AccessLevel.Editor)) });

        Assert.Equal(AccessLevel.None, (await Resolver(world, source).ResolveAsync(User("cora", "Customer"), Ref("chair.psd"), TestContext.Current.CancellationToken)).Level);
    }

    [Fact]
    public async Task AContributedBlockNarrowsLikeAStoredOne()
    {
        var world = Example();
        var source = new Source("compliance", new() { ["images"] = Entries(ShareEntry.Block(SharePrincipal.Role("Customer"))) });

        Assert.Equal(AccessLevel.None, (await Resolver(world, source).ResolveAsync(User("cora", "Customer"), Ref("chair-original.jpg"), TestContext.Current.CancellationToken)).Level);
    }

    [Fact]
    public async Task AContributionIsBoundByTheSameLimits()
    {
        // Anyone only ever Viewer; Admin only on a root.
        var world = Example();
        var source = new Source("loose", new()
        {
            ["images"] = Entries(ShareEntry.Grant(SharePrincipal.Anyone, AccessLevel.Manager), ShareEntry.Grant(SharePrincipal.User("x"), AccessLevel.Admin)),
        });
        var resolver = Resolver(world, source);

        Assert.Equal(AccessLevel.Viewer, (await resolver.ResolveAsync(Visitor, Ref("chair-original.jpg"), TestContext.Current.CancellationToken)).Level);
        Assert.Equal(AccessLevel.Manager, (await resolver.ResolveAsync(User("x"), Ref("chair-original.jpg"), TestContext.Current.CancellationToken)).Level);
    }

    [Fact]
    public async Task AtOneDepthAContributionComesAfterTheStoredSharing()
    {
        // Stored: Marketing is Editor on Images. A source at the same depth blocks Marketing:
        // it comes later and wins.
        var world = Example();
        var source = new Source("freeze", new() { ["images"] = Entries(ShareEntry.Block(SharePrincipal.Role("Marketing"))) });

        Assert.Equal(AccessLevel.None, (await Resolver(world, source).ResolveAsync(User("mia", "Marketing"), Ref("chair-original.jpg"), TestContext.Current.CancellationToken)).Level);
    }

    [Fact]
    public async Task AHigherPrioritySourceHasTheLastWordAtOneDepth()
    {
        var world = Example();
        var low = new LowSource("low", new() { ["images"] = Entries(ShareEntry.Block(SharePrincipal.User("dana"))) });
        var high = new Source("high", new() { ["images"] = Entries(ShareEntry.Grant(SharePrincipal.User("dana"), AccessLevel.Commenter)) });
        var priority = new SharingServiceTests.Priorities { [typeof(Source)] = 10 };

        // Registered high first; priority, not order, puts it last.
        var resolver = new SharingResolver(world.Kinds(), world.Store, world.Clock, [high, low], priority);

        Assert.Equal(AccessLevel.Commenter, (await resolver.ResolveAsync(User("dana"), Ref("chair-original.jpg"), TestContext.Current.CancellationToken)).Level);
    }

    [Fact]
    public async Task AContributedRuleAppliesBelowItsObjectLikeAStoredRule()
    {
        var world = Example();
        var source = new Source("dlp", new()
        {
            // On Spec sheets: below the drive's stored Staff grant, so deeper and decisive there.
            ["spec-sheets"] = new ShareContribution([], [new ShareRule("dlp-1", "no psd for staff", [new ShareCondition("extension", ShareConditionOperator.Equals, ["psd"])], [ShareEntry.Block(SharePrincipal.Role("Staff"))])]),
        });
        var resolver = Resolver(world, source);

        Assert.Equal(AccessLevel.None, (await resolver.ResolveAsync(User("stan", "Staff"), Ref("spec.psd"), TestContext.Current.CancellationToken)).Level);
        Assert.Equal(AccessLevel.Viewer, (await resolver.ResolveAsync(User("stan", "Staff"), Ref("chair-original.jpg"), TestContext.Current.CancellationToken)).Level);
    }
}
