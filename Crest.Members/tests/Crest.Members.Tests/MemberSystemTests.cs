using System.Security.Claims;
using Crest.Members.Constants;
using Crest.Members.Indexes;
using Crest.Members.Models;
using Crest.Members.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;
using OrchardCore.Entities;
using OrchardCore.Security;
using OrchardCore.Users.Models;
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

public class MemberPermissionCeilingTests
{
    private static MemberPermissionCeilingHandler Handler(Action<MemberPermissionCeilingOptions>? configure = null)
    {
        var options = new MemberPermissionCeilingOptions()
            .Ceiling("ManageTenants", "ManageSettings")
            .CeilingPrefix("ManageUsersInRole_");
        configure?.Invoke(options);
        return new MemberPermissionCeilingHandler(Options.Create(options));
    }

    private static AuthorizationHandlerContext Context(string userClass, params string[] permissionNames)
    {
        var identity = new ClaimsIdentity("Test");
        identity.AddClaim(new Claim(MemberClaims.UserClass, userClass));
        var requirements = permissionNames
            .Select(name => new PermissionRequirement(new OrchardCore.Security.Permissions.Permission(name)))
            .Cast<IAuthorizationRequirement>()
            .ToArray();
        return new AuthorizationHandlerContext(requirements, new ClaimsPrincipal(identity), resource: null);
    }

    [Fact]
    public async Task Member_CeilingedPermission_Fails()
    {
        var context = Context(UserClasses.Member, "ManageTenants");
        await Handler().HandleAsync(context);
        Assert.True(context.HasFailed);
    }

    [Fact]
    public async Task Member_CeilingedPermission_FailsEvenAfterAnotherHandlerSucceeded()
    {
        // The SuperUserHandler scenario: a blanket Succeed must not survive the veto.
        var context = Context(UserClasses.Member, "ManageSettings");
        foreach (var requirement in context.Requirements.OfType<PermissionRequirement>())
        {
            context.Succeed(requirement);
        }

        await Handler().HandleAsync(context);
        Assert.True(context.HasFailed);
    }

    [Fact]
    public async Task Member_DynamicVariant_FailsViaPrefix()
    {
        var context = Context(UserClasses.Member, "ManageUsersInRole_Editor");
        await Handler().HandleAsync(context);
        Assert.True(context.HasFailed);
    }

    [Fact]
    public async Task Member_UnceilingedPermission_Unaffected()
    {
        var context = Context(UserClasses.Member, "ListContent");
        await Handler().HandleAsync(context);
        Assert.False(context.HasFailed);
    }

    [Fact]
    public async Task Staff_CeilingedPermission_Unaffected()
    {
        var context = Context(UserClasses.Staff, "ManageTenants");
        await Handler().HandleAsync(context);
        Assert.False(context.HasFailed);
    }

    [Fact]
    public async Task MissingClassClaim_TreatedAsStaff()
    {
        var identity = new ClaimsIdentity("Test");
        var context = new AuthorizationHandlerContext(
            [new PermissionRequirement(new OrchardCore.Security.Permissions.Permission("ManageTenants"))],
            new ClaimsPrincipal(identity),
            resource: null);
        await Handler().HandleAsync(context);
        Assert.False(context.HasFailed);
    }

    [Fact]
    public async Task Unauthenticated_Unaffected()
    {
        var context = new AuthorizationHandlerContext(
            [new PermissionRequirement(new OrchardCore.Security.Permissions.Permission("ManageTenants"))],
            new ClaimsPrincipal(new ClaimsIdentity()),
            resource: null);
        await Handler().HandleAsync(context);
        Assert.False(context.HasFailed);
    }
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
