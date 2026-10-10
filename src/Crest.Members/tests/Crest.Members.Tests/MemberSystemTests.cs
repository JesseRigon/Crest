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

public class HierarchyPathMathTests
{
    [Fact]
    public void BuildPath_RootLevel_IsSlashIdSlash()
        => Assert.Equal("/12/", HierarchyPathMath.BuildPath(null, 12));

    [Fact]
    public void BuildPath_Child_AppendsToParent()
        => Assert.Equal("/12/45/", HierarchyPathMath.BuildPath("/12/", 45));

    [Fact]
    public void Chain_ReturnsRootFirstSelfLast()
        => Assert.Equal([12L, 45L, 78L], HierarchyPathMath.Chain("/12/45/78/"));

    [Fact]
    public void IsWithin_SelfAndDescendants_True()
    {
        Assert.True(HierarchyPathMath.IsWithin("/12/45/", "/12/45/"));
        Assert.True(HierarchyPathMath.IsWithin("/12/45/78/", "/12/45/"));
        Assert.False(HierarchyPathMath.IsWithin("/12/450/", "/12/45/"));
        Assert.False(HierarchyPathMath.IsWithin("/12/", "/12/45/"));
    }

    [Fact]
    public void IsWithin_SiblingPrefixNumbers_DoNotCollide()
        // "/1/" must not match inside "/12/..." - the trailing separator prevents it.
        => Assert.False(HierarchyPathMath.IsWithin("/12/45/", "/1/"));

    [Fact]
    public void Reroot_RewritesMovedSubtreePaths()
        => Assert.Equal("/99/45/78/", HierarchyPathMath.Reroot("/12/45/78/", "/12/45/", "/99/45/"));
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
    public void UserWithoutAspect_MapsToStaff()
    {
        var user = new User { UserId = "u1" };
        Assert.False(user.TryGet<CrestUserClass>(out _));
    }

    [Fact]
    public void MemberAspect_RoundTrips()
    {
        var user = new User { UserId = "u1" };
        user.Put(new CrestUserClass { Class = UserClasses.Member });
        Assert.True(user.TryGet<CrestUserClass>(out var userClass));
        Assert.Equal(UserClasses.Member, userClass!.Class);
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
