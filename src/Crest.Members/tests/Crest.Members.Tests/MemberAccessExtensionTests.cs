using Crest.Access;
using Crest.Members.Constants;
using Crest.Members.Services;
using Xunit;

namespace Crest.Members.Tests;

// Members on the access extension system (docs/access.md, Domains onto the extension system):
// ManageMembers is decided against an organization or a binding, and the binding rows a
// caller reads come from the binding scope.
public class MemberAccessExtensionTests
{
    private static readonly CallerContext Staff = new() { Tenant = "t", Side = CallerSide.Admin, UserId = "staff-1", UserClass = UserClasses.Staff };
    private static readonly CallerContext MemberOfA = new() { Tenant = "t", Side = CallerSide.Member, UserId = "m-1", UserClass = UserClasses.Member, OrganizationId = "org-a" };
    private static readonly CallerContext Anonymous = CallerContext.Anonymous("t", CallerSide.Site);

    [Fact]
    public async Task Staff_answer_with_the_tenant_wide_permission()
    {
        var mapper = new MemberResourcePermissionMapper(null!);
        var candidates = await mapper.MapAsync("ManageMembers", new MemberOrganizationResource("org-a"), Staff, TestContext.Current.CancellationToken);
        Assert.Equal(["ManageMembers"], candidates!.Select(candidate => candidate.Permission));
    }

    [Fact]
    public async Task A_caller_acting_in_an_organization_answers_only_for_its_own()
    {
        var mapper = new MemberResourcePermissionMapper(null!);
        var own = await mapper.MapAsync("ManageMembers", new MemberOrganizationResource("org-a"), MemberOfA, TestContext.Current.CancellationToken);
        var other = await mapper.MapAsync("ManageMembers", new MemberOrganizationResource("org-b"), MemberOfA, TestContext.Current.CancellationToken);
        var otherBinding = await mapper.MapAsync("ManageMembers", new MemberBindingResource("u-9", "org-b"), MemberOfA, TestContext.Current.CancellationToken);

        Assert.Equal(["ManageMembers"], own!.Select(candidate => candidate.Permission));
        Assert.Empty(other!);
        Assert.Empty(otherBinding!);
    }

    [Fact]
    public async Task Other_permissions_and_resources_are_not_claimed()
    {
        var mapper = new MemberResourcePermissionMapper(null!);
        Assert.Null(await mapper.MapAsync("ViewContent", new MemberOrganizationResource("org-a"), Staff, TestContext.Current.CancellationToken));
        Assert.Null(await mapper.MapAsync("ManageMembers", "org-a", Staff, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task The_binding_scope_limits_a_member_to_its_organization_and_refuses_anonymous()
    {
        var provider = new UserConnectionScopeProvider();

        Assert.Equal(ScopeKind.All, (await provider.BuildAsync(Staff, TestContext.Current.CancellationToken)).Kind);
        Assert.Equal(ScopeKind.None, (await provider.BuildAsync(Anonymous, TestContext.Current.CancellationToken)).Kind);

        var member = await provider.BuildAsync(MemberOfA, TestContext.Current.CancellationToken);
        Assert.Equal(ScopeKind.Filter, member.Kind);
        Assert.Equal("org-a", member.Filter!.Condition!.Values.Single());

        // A member off the member side holds no organization rows.
        var onSite = new CallerContext { Tenant = "t", Side = CallerSide.Site, UserId = "m-1", UserClass = UserClasses.Member, OrganizationId = "org-a" };
        Assert.Equal(ScopeKind.None, (await provider.BuildAsync(onSite, TestContext.Current.CancellationToken)).Kind);
    }
}
