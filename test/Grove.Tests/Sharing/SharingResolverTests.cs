using Grove.Access;
using Grove.Access.Sharing;
using static Grove.Tests.Sharing.SharingWorld;

namespace Grove.Tests.Sharing;

/// <summary>
/// The resolution engine against docs/sharing.md: the worked example line by line, then each
/// requirement of the engine (49-65) on its own.
/// </summary>
public class SharingResolverTests
{
    private static async Task<AccessLevel> LevelAsync(SharingWorld world, CallerContext caller, string id) =>
        (await world.Resolver().ResolveAsync(caller, Ref(id), TestContext.Current.CancellationToken)).Level;

    // The example's people.
    private static readonly CallerContext Customer = User("cora", "Customer");
    private static readonly CallerContext Staff = User("stan", "Staff");
    private static readonly CallerContext Marketer = User("mia", "Marketing");
    private static readonly CallerContext Dana = User("dana");
    private static readonly CallerContext Sam = User("sam", "Staff");

    [Fact]
    public async Task NobodyElseGetsIntoTheDrive()
    {
        // Access is denied by default: no grant, no access, no block needed.
        Assert.Equal(AccessLevel.None, await LevelAsync(Example(), User("other"), "product-info"));
        Assert.Equal(AccessLevel.None, await LevelAsync(Example(), Visitor, "product-info"));
    }

    [Fact]
    public async Task CustomersAndStaffViewTheDrive()
    {
        var world = Example();

        Assert.Equal(AccessLevel.Viewer, await LevelAsync(world, Customer, "chair-original.jpg"));
        Assert.Equal(AccessLevel.Viewer, await LevelAsync(world, Staff, "chair.psd"));
    }

    [Fact]
    public async Task SpecSheetsIsOpenToTheWholeTenantSoDanaSeesIt()
    {
        var world = Example();

        Assert.Equal(AccessLevel.Viewer, await LevelAsync(world, Dana, "spec-sheets"));
        Assert.Equal(AccessLevel.Viewer, await LevelAsync(world, Dana, "spec.psd"));
    }

    [Fact]
    public async Task SamsUserBlockOutranksTheTenantGrantAtAnyDepth()
    {
        var world = Example();

        Assert.Equal(AccessLevel.None, await LevelAsync(world, Sam, "spec-sheets"));
        // ...and his Staff role's grant on the drive.
        Assert.Equal(AccessLevel.None, await LevelAsync(world, Sam, "chair-original.jpg"));
    }

    [Fact]
    public async Task OnlyAUserGrantDeeperLetsSamIn()
    {
        var world = Example();
        world.Share("spec-sheets", ShareEntry.Grant(SharePrincipal.User("sam"), AccessLevel.Viewer));

        Assert.Equal(AccessLevel.Viewer, await LevelAsync(world, Sam, "spec-sheets"));
    }

    [Fact]
    public async Task ImagesAddsMarketingOnTopOfTheInheritedGrants()
    {
        var world = Example();

        Assert.Equal(AccessLevel.Editor, await LevelAsync(world, Marketer, "chair-original.jpg"));
        Assert.Equal(AccessLevel.Viewer, await LevelAsync(world, Customer, "chair-original.jpg"));
        // Marketing's grant stays under Images: on Spec sheets a marketer has only what the whole
        // tenant has there (requirement 37: Tenant is everyone signed in).
        Assert.Equal(AccessLevel.Viewer, await LevelAsync(world, Marketer, "spec-sheets"));
    }

    [Fact]
    public async Task RuleOneBlocksCustomersAndGrantsDanaOnPhotoshopFilesUnderImagesOnly()
    {
        var world = Example();

        Assert.Equal(AccessLevel.None, await LevelAsync(world, Customer, "chair.psd"));
        Assert.Equal(AccessLevel.Editor, await LevelAsync(world, Dana, "chair.psd"));
        Assert.Equal(AccessLevel.Editor, await LevelAsync(world, Marketer, "chair.psd"));

        // A Photoshop file in Spec sheets is untouched; elsewhere under Images Dana has no grant.
        Assert.Equal(AccessLevel.Viewer, await LevelAsync(world, Customer, "spec.psd"));
        Assert.Equal(AccessLevel.Viewer, await LevelAsync(world, Dana, "spec.psd"));
        Assert.Equal(AccessLevel.None, await LevelAsync(world, Dana, "chair-original.jpg"));
    }

    [Fact]
    public async Task RuleTwoMakesFinalImagesPublicAndPublicIsAbsoluteForViewingOnly()
    {
        var world = Example();
        var resolver = world.Resolver();

        var forVisitor = await resolver.ResolveAsync(Visitor, Ref("chair-final.jpg"), TestContext.Current.CancellationToken);
        var forSam = await resolver.ResolveAsync(Sam, Ref("chair-final.jpg"), TestContext.Current.CancellationToken);

        Assert.True(forVisitor.IsPublic);
        Assert.Equal(AccessLevel.Viewer, forVisitor.Level);
        // Sam can view it despite his block; his block still stops him commenting or editing.
        Assert.Equal(AccessLevel.Viewer, forSam.Level);
        Assert.False(forSam.Allows(AccessLevel.Commenter));
        // The original is not public.
        Assert.Equal(AccessLevel.None, await LevelAsync(world, Visitor, "chair-original.jpg"));
    }

    [Fact]
    public async Task ARuleDoesNotApplyToTheObjectItIsDefinedOn()
    {
        var world = Example();
        world.Files.Add("images", "product-info", tag: "final");

        Assert.Equal(AccessLevel.None, await LevelAsync(world, Visitor, "images"));
    }

    [Fact]
    public async Task WithinATierTheDeepestDecisionWins()
    {
        // Two roles both blocked on a folder; a subfolder grants one of them (requirement 60).
        var world = new SharingWorld();
        world.Files.Add("drive");
        world.Files.Add("folder", "drive");
        world.Files.Add("sub", "folder");
        world.Share("drive", ShareEntry.Grant(SharePrincipal.Role("Customer"), AccessLevel.Viewer), ShareEntry.Grant(SharePrincipal.Role("Vendor"), AccessLevel.Viewer));
        world.Share("folder", ShareEntry.Block(SharePrincipal.Role("Customer")), ShareEntry.Block(SharePrincipal.Role("Vendor")));
        world.Share("sub", ShareEntry.Grant(SharePrincipal.Role("Vendor"), AccessLevel.Viewer));
        var both = User("v", "Customer", "Vendor");

        Assert.Equal(AccessLevel.None, await LevelAsync(world, both, "folder"));
        Assert.Equal(AccessLevel.Viewer, await LevelAsync(world, both, "sub"));
    }

    [Fact]
    public async Task AUserBlockedOnAFolderAndGrantedOnAFileInsideCanOpenTheFile()
    {
        var world = new SharingWorld();
        world.Files.Add("drive");
        world.Files.Add("folder", "drive");
        world.Files.Add("file", "folder");
        world.Share("folder", ShareEntry.Block(SharePrincipal.User("u")));
        world.Share("file", ShareEntry.Grant(SharePrincipal.User("u"), AccessLevel.Viewer));

        Assert.Equal(AccessLevel.None, await LevelAsync(world, User("u"), "folder"));
        Assert.Equal(AccessLevel.Viewer, await LevelAsync(world, User("u"), "file"));
    }

    [Fact]
    public async Task WhenAPersonsRolesDisagreeAtOneDepthTheGrantWinsAndGrantsStack()
    {
        var world = new SharingWorld();
        world.Files.Add("drive");
        world.Share("drive", ShareEntry.Block(SharePrincipal.Role("A")), ShareEntry.Grant(SharePrincipal.Role("B"), AccessLevel.Viewer), ShareEntry.Grant(SharePrincipal.Role("C"), AccessLevel.Editor));

        Assert.Equal(AccessLevel.Editor, await LevelAsync(world, User("u", "A", "B", "C"), "drive"));
        Assert.Equal(AccessLevel.None, await LevelAsync(world, User("u", "A"), "drive"));
    }

    [Fact]
    public async Task ALaterRuleOverridesAnEarlierOneAboutTheSamePrincipal()
    {
        var world = new SharingWorld();
        world.Files.Add("drive");
        world.Files.Add("doc.pdf", "drive", extension: "pdf");
        var pdf = new ShareCondition("extension", ShareConditionOperator.Equals, ["pdf"]);
        world.Rule("drive", new ShareRule("r1", "grant", [pdf], [ShareEntry.Grant(SharePrincipal.Role("R"), AccessLevel.Viewer)]));
        world.Rule("drive", new ShareRule("r2", "block", [pdf], [ShareEntry.Block(SharePrincipal.Role("R"))]));

        Assert.Equal(AccessLevel.None, await LevelAsync(world, User("u", "R"), "doc.pdf"));
    }

    [Fact]
    public async Task AnObjectsOwnEntryOverridesARuleDefinedAtTheSameDepth()
    {
        var world = new SharingWorld();
        world.Files.Add("drive");
        world.Files.Add("folder", "drive", extension: "x");
        world.Files.Add("file", "folder", extension: "pdf");
        world.Rule("folder", new ShareRule("r", "block pdf", [new ShareCondition("extension", ShareConditionOperator.Equals, ["pdf"])], [ShareEntry.Block(SharePrincipal.Role("R"))]));
        world.Share("folder", ShareEntry.Grant(SharePrincipal.Role("R"), AccessLevel.Viewer));

        // The rule (defined on folder) and folder's own entry are at the same depth; for the file
        // below, the entry is later in order and wins.
        Assert.Equal(AccessLevel.Viewer, await LevelAsync(world, User("u", "R"), "file"));
    }

    [Fact]
    public async Task AnyoneIsNeverMoreThanAViewerAndAdminCountsOnlyOnARoot()
    {
        var world = new SharingWorld();
        world.Files.Add("drive");
        world.Files.Add("folder", "drive");
        world.Share("drive", ShareEntry.Grant(SharePrincipal.Anyone, AccessLevel.Manager));
        world.Share("folder", ShareEntry.Grant(SharePrincipal.User("u"), AccessLevel.Admin));

        Assert.Equal(AccessLevel.Viewer, await LevelAsync(world, Visitor, "folder"));
        Assert.Equal(AccessLevel.Manager, await LevelAsync(world, User("u"), "folder"));
    }

    [Fact]
    public async Task AnAdminGrantOnTheRootCoversTheWholeTree()
    {
        var world = new SharingWorld();
        world.Files.Add("drive");
        world.Files.Add("folder", "drive");
        world.Files.Add("file", "folder");
        world.Share("drive", ShareEntry.Grant(SharePrincipal.User("da"), AccessLevel.Admin));

        Assert.Equal(AccessLevel.Admin, await LevelAsync(world, User("da"), "file"));
    }

    [Fact]
    public async Task TheOwnerIsAlwaysAllowed()
    {
        var world = new SharingWorld();
        world.Files.Add("drive");
        world.Files.Add("mine", "drive", owner: "u");
        world.Share("drive", ShareEntry.Block(SharePrincipal.User("u")));

        Assert.Equal(AccessLevel.Manager, await LevelAsync(world, User("u"), "mine"));
    }

    [Fact]
    public async Task APersonalDrivesOwnerAdministersEverythingInIt()
    {
        var world = new SharingWorld();
        world.Files.Add("my-drive", owner: "u");
        world.Files.Add("folder", "my-drive");
        world.Files.Add("file", "folder");

        Assert.Equal(AccessLevel.Admin, await LevelAsync(world, User("u"), "file"));
        Assert.Equal(AccessLevel.None, await LevelAsync(world, User("someone"), "file"));
    }

    [Fact]
    public async Task ATimeBoundShareStartsAndEndsOnTime()
    {
        var world = new SharingWorld();
        world.Files.Add("drive");
        var now = new DateTimeOffset(world.Clock.UtcNow, TimeSpan.Zero);
        world.Share("drive", ShareEntry.Grant(SharePrincipal.User("u"), AccessLevel.Viewer, starts: now.AddHours(1), ends: now.AddHours(2)));

        Assert.Equal(AccessLevel.None, await LevelAsync(world, User("u"), "drive"));
        world.Clock.UtcNow = world.Clock.UtcNow.AddMinutes(90);
        Assert.Equal(AccessLevel.Viewer, await LevelAsync(world, User("u"), "drive"));
        world.Clock.UtcNow = world.Clock.UtcNow.AddMinutes(30);
        Assert.Equal(AccessLevel.None, await LevelAsync(world, User("u"), "drive"));
    }

    [Fact]
    public async Task ARuleWithATimeWindowAppliesOnlyInsideIt()
    {
        var world = new SharingWorld();
        world.Files.Add("drive");
        world.Files.Add("file", "drive", extension: "pdf");
        var now = new DateTimeOffset(world.Clock.UtcNow, TimeSpan.Zero);
        world.Rule("drive", new ShareRule("embargo", "public after launch", [], [ShareEntry.Grant(SharePrincipal.Anyone, AccessLevel.Viewer)], Starts: now.AddDays(1)));

        Assert.Equal(AccessLevel.None, await LevelAsync(world, Visitor, "file"));
        world.Clock.UtcNow = world.Clock.UtcNow.AddDays(2);
        Assert.Equal(AccessLevel.Viewer, await LevelAsync(world, Visitor, "file"));
    }

    [Fact]
    public async Task TenantLevelEntriesAndRulesReachEveryObject()
    {
        var world = new SharingWorld();
        world.Files.Add("drive");
        world.Files.Add("file", "drive", extension: "pdf");
        world.Share(null, ShareEntry.Grant(SharePrincipal.Role("Auditor"), AccessLevel.Viewer));

        Assert.Equal(AccessLevel.Viewer, await LevelAsync(world, User("a", "Auditor"), "file"));
    }

    [Fact]
    public async Task TenantMeansSignedInNotAnyone()
    {
        var world = new SharingWorld();
        world.Files.Add("drive");
        world.Share("drive", ShareEntry.Grant(SharePrincipal.Tenant, AccessLevel.Viewer));

        Assert.Equal(AccessLevel.Viewer, await LevelAsync(world, User("u"), "drive"));
        Assert.Equal(AccessLevel.None, await LevelAsync(world, Visitor, "drive"));
    }

    [Fact]
    public async Task AMemberCountsOnlyForTheOrganizationTheyActFor()
    {
        var world = new SharingWorld();
        world.Files.Add("org-drive");
        world.Share("org-drive", ShareEntry.Grant(SharePrincipal.Organization("acme"), AccessLevel.Editor));

        Assert.Equal(AccessLevel.Editor, await LevelAsync(world, Member("m", "acme"), "org-drive"));
        Assert.Equal(AccessLevel.None, await LevelAsync(world, Member("m", "globex"), "org-drive"));
    }

    [Fact]
    public async Task AnInactiveDriveAllowsNothingWhateverTheGrant()
    {
        var world = new SharingWorld();
        world.Files.Add("drive", inactive: true);
        world.Files.Add("file", "drive");
        world.Share("drive", ShareEntry.Grant(SharePrincipal.User("u"), AccessLevel.Admin));

        var resolution = await world.Resolver().ResolveAsync(User("u"), Ref("file"), TestContext.Current.CancellationToken);

        Assert.True(resolution.IsInactive);
        Assert.False(resolution.Allows(AccessLevel.Viewer));
    }

    [Fact]
    public async Task AnUnknownObjectOrALoopingChainIsNoAccess()
    {
        var world = new SharingWorld();
        world.Files.Add("a", "b");
        world.Files.Add("b", "a");

        Assert.Equal(AccessLevel.None, await LevelAsync(world, User("u"), "missing"));
        Assert.Equal(AccessLevel.None, await LevelAsync(world, User("u"), "a"));
    }

    [Fact]
    public async Task ResolutionIsFullyDeterminedByThePathThePrincipalsAndTheRuleOrder()
    {
        // Requirement 65: the same inputs give the same answer, every time.
        var world = Example();
        foreach (var caller in new[] { Customer, Staff, Marketer, Dana, Sam, Visitor })
        {
            foreach (var id in world.Files.Nodes.Keys)
            {
                Assert.Equal(await LevelAsync(world, caller, id), await LevelAsync(world, caller, id));
            }
        }
    }
}
