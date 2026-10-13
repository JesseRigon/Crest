using System.Security.Claims;
using Crest.Members.Constants;
using Crest.Members.Indexes;
using Crest.Members.Models;
using Crest.Members.Services;
using Microsoft.Extensions.Options;
using Crest.Entities;
using Crest.Security;
using Crest.Users.Models;
using Xunit;

namespace Crest.Members.Tests;

public class HierarchyClosureTests
{
    private static Dictionary<string, List<string>> Edges(params (string Report, string Manager)[] edges)
    {
        var managers = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        foreach (var (report, manager) in edges)
        {
            (managers.TryGetValue(report, out var list) ? list : managers[report] = []).Add(manager);
            managers.TryAdd(manager, []);
        }

        return managers;
    }

    [Fact]
    public void Ancestors_FollowEveryPath()
    {
        // d reports to b and c; both report to a.
        var ancestors = HierarchyClosure.ComputeAncestors(Edges(("b", "a"), ("c", "a"), ("d", "b"), ("d", "c")));

        Assert.Empty(ancestors["a"]);
        Assert.Equal(["a"], ancestors["b"]);
        Assert.Equal(["a", "b", "c"], ancestors["d"].Order(StringComparer.Ordinal));
    }

    [Fact]
    public void WouldCycle_RefusesAReportAlreadyAboveTheManager()
    {
        var edges = Edges(("b", "a"), ("c", "b"));

        Assert.True(HierarchyClosure.WouldCycle(edges, reportId: "a", managerId: "c"));
        Assert.True(HierarchyClosure.WouldCycle(edges, reportId: "a", managerId: "a"));
        Assert.False(HierarchyClosure.WouldCycle(edges, reportId: "c", managerId: "a"));
    }

    [Fact]
    public void ComputeAncestors_ThrowsOnAnExistingCycle()
        => Assert.Throws<InvalidOperationException>(() => HierarchyClosure.ComputeAncestors(Edges(("a", "b"), ("b", "a"))));

    [Fact]
    public void Chains_ListEveryPathTopFirst()
    {
        var chains = HierarchyClosure.Chains(Edges(("b", "a"), ("c", "a"), ("d", "b"), ("d", "c")), "d");

        Assert.Equal(2, chains.Count);
        Assert.Contains(chains, chain => chain.SequenceEqual(["a", "b", "d"]));
        Assert.Contains(chains, chain => chain.SequenceEqual(["a", "c", "d"]));
    }

    [Fact]
    public void Chains_OfATop_IsItself()
        => Assert.Equal(["a"], Assert.Single(HierarchyClosure.Chains(Edges(("b", "a")), "a")));
}

public class MemberClassCeilingTests
{
    private static MemberClassCeiling Ceiling()
    {
        var options = new MemberPermissionCeilingOptions()
            .Ceiling("ManageTenants", "ManageSettings")
            .CeilingPrefix("ManageUsersInRole_");
        return new MemberClassCeiling(Options.Create(options));
    }

    private static Crest.Access.CallerContext Caller(string? userClass) => new()
    {
        Tenant = "Default",
        Side = Crest.Access.CallerSide.Member,
        UserId = "user-1",
        UserClass = userClass,
    };

    [Fact]
    public void Member_CeilingedPermission_IsDenied()
        => Assert.NotNull(Ceiling().Deny(Caller(UserClasses.Member), "ManageTenants"));

    [Fact]
    public void Member_DynamicVariant_IsDeniedViaPrefix()
        => Assert.NotNull(Ceiling().Deny(Caller(UserClasses.Member), "ManageUsersInRole_Editor"));

    [Fact]
    public void Member_UnceilingedPermission_Unaffected()
        => Assert.Null(Ceiling().Deny(Caller(UserClasses.Member), "ListContent"));

    [Fact]
    public void Staff_CeilingedPermission_Unaffected()
        => Assert.Null(Ceiling().Deny(Caller(UserClasses.Staff), "ManageTenants"));

    [Fact]
    public void MissingClass_TreatedAsStaff()
        => Assert.Null(Ceiling().Deny(Caller(null), "ManageTenants"));
}

public class UserIndexProviderTests
{
    private static User UserWith(object? aspect, string aspectName)
    {
        var user = new User { UserId = "u1", UserName = "u1" };
        if (aspect is not null)
        {
            user.Properties[aspectName] = System.Text.Json.JsonSerializer.SerializeToNode(aspect)!.AsObject();
        }

        return user;
    }

    [Fact]
    public void UserWithoutBindings_HasNoClass()
    {
        // No user exists outside an organization: a user with no binding is the failure
        // mode the rebuild repairs, never a default class.
        var user = new User { UserId = "u1" };
        Assert.Empty(UserClassSet.Classes(user));
    }

    [Fact]
    public void ClassIsASet_DerivedFromTheBindings()
    {
        var user = new User { UserId = "u1" };
        user.Put(new CrestMemberInfo
        {
            Bindings =
            [
                new MemberOrgBinding { OrganizationId = "tenant", Class = UserClasses.Staff },
                new MemberOrgBinding { OrganizationId = "orgA", Class = UserClasses.Member, Roles = [MemberRoleTemplates.Member] },
            ],
        });

        Assert.Equal([UserClasses.Staff, UserClasses.Member], UserClassSet.Classes(user));
        Assert.True(UserClassSet.Has(user, UserClasses.Staff));
        Assert.Equal("orgA", Assert.Single(UserClassSet.Bindings(user, UserClasses.Member)).OrganizationId);
    }

    [Fact]
    public void BindingsAspect_RoundTripsWithRolesAndAdminFlag()
    {
        var user = new User { UserId = "u1" };
        user.Put(new CrestMemberInfo
        {
            PersonId = "p1",
            Bindings =
            [
                new MemberOrgBinding { OrganizationId = "orgA", Roles = [MemberRoleTemplates.MemberAdministrator], IsMemberAdmin = true },
                new MemberOrgBinding { OrganizationId = "orgB", Roles = [MemberRoleTemplates.Member] },
            ],
        });

        Assert.True(user.TryGet<CrestMemberInfo>(out var info));
        Assert.Equal(2, info!.Bindings.Count);
        Assert.True(info.Bindings[0].IsMemberAdmin);
        Assert.False(info.Bindings[1].IsMemberAdmin);
        Assert.Equal("p1", info.PersonId);
    }
}

public class MemberPortalLoginContextTests
{
    private sealed class NoHttpContextAccessor : Microsoft.AspNetCore.Http.IHttpContextAccessor
    {
        public Microsoft.AspNetCore.Http.HttpContext? HttpContext { get; set; }
    }

    [Fact]
    public async Task Resolve_WithoutRequest_IsTenantSurface()
    {
        var context = new MemberPortalLoginContext(new NoHttpContextAccessor());

        Assert.Null(await context.ResolveAsync());
    }

    [Fact]
    public async Task MarkPortal_WinsOverResolution_AndCarriesOrgAndPerson()
    {
        var context = new MemberPortalLoginContext(new NoHttpContextAccessor());
        context.MarkPortal("org-1", "person-1");

        var marker = await context.ResolveAsync();

        Assert.NotNull(marker);
        Assert.Equal("org-1", marker.OrganizationId);
        Assert.Equal("person-1", marker.PersonId);
    }

    [Fact]
    public async Task MarkPortal_WithoutOrg_IsPortalSurfaceForAnyBinding()
    {
        var context = new MemberPortalLoginContext(new NoHttpContextAccessor());
        context.MarkPortal(null);

        var marker = await context.ResolveAsync();

        Assert.NotNull(marker);
        Assert.Null(marker.OrganizationId);
    }
}
