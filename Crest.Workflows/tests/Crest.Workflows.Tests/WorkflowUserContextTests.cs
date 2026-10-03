using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using NSubstitute;
using Crest.Workflows.Contexts;
using OrchardCore.Security;
using OrchardCore.Security.Permissions;
using Xunit;

namespace Crest.Workflows.Tests;

// The acting-user snapshot: what a trigger captures in the request must rebuild into a
// principal that Orchard's authorization sees exactly as the original - every claim,
// including the ones other modules add (the user class and active organization),
// so their handlers keep working on the background side.
public class WorkflowUserContextTests
{
    [Fact]
    public void Snapshot_round_trips_every_claim_and_the_identity()
    {
        var principal = new ClaimsPrincipal(new ClaimsIdentity(
        [
            new Claim(ClaimTypes.Name, "alice"),
            new Claim(ClaimTypes.NameIdentifier, "u-1"),
            new Claim(ClaimTypes.Role, "Editor"),
            new Claim("crest_class", "member"),
            new Claim("crest_active_org", "org-9"),
        ], "Identity.Application"));

        var snapshot = WorkflowUserContext.From(principal, "acme");
        var rebuilt = snapshot.ToPrincipal();

        Assert.True(snapshot.IsAuthenticated);
        Assert.Equal("acme", snapshot.Tenant);
        Assert.Equal("alice", snapshot.UserName);
        Assert.Equal("u-1", snapshot.UserId);
        Assert.True(rebuilt.Identity!.IsAuthenticated);
        Assert.Equal("alice", rebuilt.Identity.Name);
        Assert.True(rebuilt.IsInRole("Editor"));
        Assert.Equal("member", rebuilt.FindFirst("crest_class")?.Value);
        Assert.Equal("org-9", rebuilt.FindFirst("crest_active_org")?.Value);
    }

    [Fact]
    public void Anonymous_stays_anonymous()
    {
        var snapshot = WorkflowUserContext.From(new ClaimsPrincipal(new ClaimsIdentity()), "acme");

        Assert.False(snapshot.IsAuthenticated);
        Assert.False(snapshot.ToPrincipal().Identity!.IsAuthenticated);
        Assert.Empty(snapshot.Claims);
    }

    [Fact]
    public async Task Authorizer_denies_anonymous_blank_and_unknown_permissions_without_asking_authorization()
    {
        var authorization = Substitute.For<IAuthorizationService>();
        var permissions = Substitute.For<IPermissionService>();
        permissions.FindByNameAsync("Nope").Returns(new ValueTask<Permission>((Permission)null!));
        var authorizer = new WorkflowAuthorizer(authorization, permissions);
        var user = WorkflowUserContext.From(new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.Name, "alice")], "Test")), "acme");

        Assert.False(await authorizer.AuthorizeAsync(null, "ManageWorkflows", TestContext.Current.CancellationToken));
        Assert.False(await authorizer.AuthorizeAsync(WorkflowUserContext.Anonymous("acme"), "ManageWorkflows", TestContext.Current.CancellationToken));
        Assert.False(await authorizer.AuthorizeAsync(user, " ", TestContext.Current.CancellationToken));
        Assert.False(await authorizer.AuthorizeAsync(user, "Nope", TestContext.Current.CancellationToken));
        await authorization.DidNotReceive().AuthorizeAsync(Arg.Any<ClaimsPrincipal>(), Arg.Any<object?>(), Arg.Any<IEnumerable<IAuthorizationRequirement>>());
    }

    [Fact]
    public async Task Authorizer_evaluates_a_known_permission_against_the_rebuilt_principal()
    {
        var permission = new Permission("PublishContent", "Publish content");
        var authorization = Substitute.For<IAuthorizationService>();
        authorization.AuthorizeAsync(Arg.Any<ClaimsPrincipal>(), Arg.Any<object?>(), Arg.Any<IEnumerable<IAuthorizationRequirement>>()).Returns(AuthorizationResult.Failed());
        authorization.AuthorizeAsync(Arg.Is<ClaimsPrincipal>(p => p!.Identity!.Name == "alice"), Arg.Any<object?>(), Arg.Is<IEnumerable<IAuthorizationRequirement>>(r => r!.OfType<PermissionRequirement>().Any(x => x.Permission == permission)))
            .Returns(AuthorizationResult.Success());
        var permissions = Substitute.For<IPermissionService>();
        permissions.FindByNameAsync("PublishContent").Returns(new ValueTask<Permission>(permission));
        var authorizer = new WorkflowAuthorizer(authorization, permissions);
        var user = WorkflowUserContext.From(new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.Name, "alice")], "Test")), "acme");

        Assert.True(await authorizer.AuthorizeAsync(user, "PublishContent", TestContext.Current.CancellationToken));
        Assert.False(await authorizer.AuthorizeAsync(WorkflowUserContext.From(new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.Name, "bob")], "Test")), "acme"), "PublishContent", TestContext.Current.CancellationToken));
    }
}
