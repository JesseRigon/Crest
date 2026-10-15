using Grove.Access;
using Grove.Members.Constants;
using Grove.Members.Indexes;
using Grove.Members.Services;
using Grove.Parties.Organizations;
using Xunit;

namespace Grove.Members.Tests;

/// <summary>
/// The conformance suite's organization fixtures (docs/workflows.md › Tasks › 1, the
/// conformance suite): organizations, callers of every side acting in or outside them, the
/// binding and content rows each organization owns, and which rows each caller's scope must
/// admit. Run against the real providers through the in-memory filter, the same rule the
/// SQL compilation applies. A provider that admits a row the table does not, fails.
/// </summary>
public class OrganizationConformanceTests
{
    private const string OrgA = "org-a";
    private const string OrgB = "org-b";

    private static CallerContext Caller(string? userId, CallerSide side, string? userClass = null, string? organization = null, bool superUser = false, bool system = false) => new()
    {
        Tenant = "t",
        Side = side,
        UserId = userId,
        UserClass = userClass,
        OrganizationId = organization,
        IsSuperUser = superUser,
        IsSystem = system,
    };

    private static readonly CallerContext Staff = Caller("staff-1", CallerSide.Admin, UserClasses.Staff);
    private static readonly CallerContext Admin = Caller("admin-1", CallerSide.Admin, UserClasses.Staff, superUser: true);
    private static readonly CallerContext MemberOfA = Caller("m-a", CallerSide.Member, UserClasses.Member, OrgA);
    private static readonly CallerContext MemberOfB = Caller("m-b", CallerSide.Member, UserClasses.Member, OrgB);
    private static readonly CallerContext MemberOfAOnSite = Caller("m-a", CallerSide.Site, UserClasses.Member, OrgA);
    private static readonly CallerContext MemberWithNoOrganization = Caller("m-x", CallerSide.Member, UserClasses.Member);
    private static readonly CallerContext SystemOfA = Caller(null, CallerSide.System, organization: OrgA, system: true);
    private static readonly CallerContext TenantSystem = Caller(null, CallerSide.System, superUser: true, system: true);
    private static readonly CallerContext Anonymous = CallerContext.Anonymous("t", CallerSide.Site);

    /// <summary>The binding rows: one per organization.</summary>
    private static readonly (string Id, string Organization)[] Bindings = [("binding-a", OrgA), ("binding-b", OrgB)];

    public sealed record Case(string Name, CallerContext Caller, string[] Admitted)
    {
        public override string ToString() => Name;
    }

    public static TheoryData<Case> BindingCases() =>
    [
        new Case("staff read every organization's bindings", Staff, ["binding-a", "binding-b"]),
        new Case("the super user reads every binding", Admin, ["binding-a", "binding-b"]),
        new Case("the tenant system actor reads every binding", TenantSystem, ["binding-a", "binding-b"]),
        new Case("a member reads their organization's bindings only", MemberOfA, ["binding-a"]),
        new Case("another organization's member reads theirs only", MemberOfB, ["binding-b"]),
        new Case("a member off the member side reads none", MemberOfAOnSite, []),
        new Case("a member acting in no organization reads none", MemberWithNoOrganization, []),
        new Case("an organization system actor reads its organization's only", SystemOfA, ["binding-a"]),
        new Case("anonymous reads none", Anonymous, []),
    ];

    [Theory]
    [MemberData(nameof(BindingCases))]
    public async Task TheBindingScopeAdmitsPerTheTable(Case @case)
    {
        var rule = await new UserConnectionScopeProvider().BuildAsync(@case.Caller, TestContext.Current.CancellationToken);

        var admitted = Bindings
            .Where(row => rule.Admits(column => column == nameof(UserConnectionIndex.OrganizationId) ? row.Organization : null))
            .Select(row => row.Id);

        Assert.Equal(@case.Admitted, admitted);
    }

    /// <summary>Content rows: an organization-scoped type and an ordinary one.</summary>
    private static readonly (string Id, string Type)[] ContentRows = [("invoice-a", "Invoice"), ("page-1", "Page")];

    public static TheoryData<Case> ContentCases() =>
    [
        new Case("staff are not organization-bound", Staff, ["invoice-a", "page-1"]),
        new Case("the super user is not organization-bound", Admin, ["invoice-a", "page-1"]),
        new Case("a visitor is not organization-bound", Anonymous, ["invoice-a", "page-1"]),
        new Case("a member off the member side sees no scoped row", MemberOfAOnSite, ["page-1"]),
        new Case("a member acting in no organization sees no scoped row", MemberWithNoOrganization, ["page-1"]),
    ];

    [Theory]
    [MemberData(nameof(ContentCases))]
    public async Task TheContentScopeAdmitsPerTheTable(Case @case)
    {
        // The cases that need no organization lookup; an organization's own rows come from
        // its index and are covered by the binding table's rule shape.
        var types = new OrganizationScopedTypes().Add("Invoice");
        var rule = await new OrganizationScopeProvider(types, null!).BuildAsync(@case.Caller, TestContext.Current.CancellationToken);

        var admitted = ContentRows
            .Where(row => rule.Admits(column => column switch
            {
                "ContentType" => row.Type,
                "ContentItemId" => row.Id,
                _ => null,
            }))
            .Select(row => row.Id);

        Assert.Equal(@case.Admitted, admitted);
    }

    public sealed record ManageCase(string Name, CallerContext Caller, string Organization, bool Answers)
    {
        public override string ToString() => Name;
    }

    public static TheoryData<ManageCase> ManageCases() =>
    [
        new ManageCase("staff answer for any organization", Staff, OrgB, true),
        new ManageCase("a member answers for their own organization", MemberOfA, OrgA, true),
        new ManageCase("a member never answers for another", MemberOfA, OrgB, false),
        new ManageCase("an organization system actor answers for its own", SystemOfA, OrgA, true),
        new ManageCase("an organization system actor never answers for another", SystemOfA, OrgB, false),
    ];

    [Theory]
    [MemberData(nameof(ManageCases))]
    public async Task ManagingAnOrganizationsMembersAnswersPerTheTable(ManageCase @case)
    {
        var candidates = await new MemberResourcePermissionMapper(null!).MapAsync(
            "ManageOrgMembers", new MemberOrganizationResource(@case.Organization), @case.Caller, TestContext.Current.CancellationToken);

        Assert.NotNull(candidates);
        Assert.Equal(@case.Answers, candidates!.Count > 0);
    }
}
