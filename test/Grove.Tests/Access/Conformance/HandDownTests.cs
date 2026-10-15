using System.Security.Claims;
using Grove.Access;
using Grove.Access.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Grove.Security;
using static Grove.Tests.Access.Conformance.AccessWorld;

namespace Grove.Tests.Access.Conformance;

/// <summary>
/// One calculation per request, handed down (docs/access.md § 2): the gate's caller is the
/// only one; the authorization handler never builds a second for a differing principal; a
/// run as another caller restores the previous one; the scope set is built once per request
/// and never shared between users.
/// </summary>
public class HandDownTests
{
    private sealed class Accessor : ICallerContextAccessor
    {
        public CallerContext Current { get; set; }
    }

    private static ClaimsPrincipal PrincipalFor(string userId) =>
        new(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, userId)], "Identity.Application"));

    private static async Task<bool> AuthorizeAsync(AccessAuthorizationHandler handler, ClaimsPrincipal principal, string permission, object resource = null)
    {
        var permissionObject = await new PermissionRegistry().FindByNameAsync(permission);
        var requirement = new PermissionRequirement(permissionObject);
        var context = new AuthorizationHandlerContext([requirement], principal, resource);
        await handler.HandleAsync(context);
        return context.HasSucceeded;
    }

    [Fact]
    public async Task APrincipalThatIsNotTheGatedCallersGetsAnonymousRights()
    {
        // A hand-built principal for the admin, presented while the editor's request runs.
        var factory = new Mock<ICallerContextFactory>(MockBehavior.Strict);
        var handler = new AccessAuthorizationHandler(new Accessor { Current = Editor }, factory.Object, Decision(), Mock.Of<IHttpContextAccessor>());

        Assert.False(await AuthorizeAsync(handler, PrincipalFor("u-admin"), "ManageUsers"));
        Assert.True(await AuthorizeAsync(handler, PrincipalFor("u-admin"), "ViewContent", HomePage));
        factory.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task TheGatedCallerAnswersForItsOwnPrincipal()
    {
        var handler = new AccessAuthorizationHandler(new Accessor { Current = Editor }, Mock.Of<ICallerContextFactory>(MockBehavior.Strict), Decision(), Mock.Of<IHttpContextAccessor>());

        Assert.True(await AuthorizeAsync(handler, PrincipalFor("u-editor"), "EditContent"));
    }

    [Fact]
    public async Task TheSystemCallerAnswersWhateverPrincipalIsPresented()
    {
        var handler = new AccessAuthorizationHandler(new Accessor { Current = SystemActor }, Mock.Of<ICallerContextFactory>(MockBehavior.Strict), Decision(), Mock.Of<IHttpContextAccessor>());

        Assert.True(await AuthorizeAsync(handler, new ClaimsPrincipal(new ClaimsIdentity()), "ManageUsers"));
    }

    [Fact]
    public async Task WithNoGateTheHandlerBuildsOnceAndHandsDown()
    {
        var accessor = new Accessor();
        var factory = new Mock<ICallerContextFactory>();
        factory.Setup(m => m.CreateAsync(It.IsAny<CallerRequest>(), It.IsAny<CancellationToken>())).ReturnsAsync(Editor);
        var handler = new AccessAuthorizationHandler(accessor, factory.Object, Decision(), Mock.Of<IHttpContextAccessor>());

        await AuthorizeAsync(handler, PrincipalFor("u-editor"), "EditContent");
        await AuthorizeAsync(handler, PrincipalFor("u-editor"), "ViewContent");

        Assert.Same(Editor, accessor.Current);
        factory.Verify(m => m.CreateAsync(It.IsAny<CallerRequest>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ARunAsAnotherCallerRestoresThePreviousOne()
    {
        var accessor = new Accessor { Current = Editor };
        var runner = new AccessRunner(accessor);
        CallerContext inside = null;

        await runner.RunAsAsync(SystemActor, () =>
        {
            inside = accessor.Current;
            return Task.CompletedTask;
        });

        Assert.Same(SystemActor, inside);
        Assert.Same(Editor, accessor.Current);
    }

    [Fact]
    public async Task ARunThatThrowsStillRestoresThePreviousCaller()
    {
        var accessor = new Accessor { Current = Editor };
        var runner = new AccessRunner(accessor);

        await Assert.ThrowsAsync<InvalidOperationException>(() => runner.RunAsAsync<int>(SystemActor, () => throw new InvalidOperationException()));

        Assert.Same(Editor, accessor.Current);
    }

    private sealed class CountingProvider(string target) : IScopeProvider
    {
        public int Builds { get; private set; }

        public string Target { get; } = target;

        public int Version => 1;

        public Task<ScopeRule> BuildAsync(CallerContext caller, CancellationToken cancellationToken = default)
        {
            Builds++;
            return Task.FromResult(caller.IsSuperUser ? ScopeRule.All : ScopeRule.None);
        }
    }

    [Fact]
    public async Task TheScopeSetIsBuiltOncePerCallerPerRequest()
    {
        var provider = new CountingProvider("ContentItemIndex");
        var scopes = new ScopeSetProvider([provider]);

        var first = await scopes.GetAsync(Editor, TestContext.Current.CancellationToken);
        var second = await scopes.GetAsync(Editor, TestContext.Current.CancellationToken);

        Assert.Same(first, second);
        Assert.Equal(1, provider.Builds);
    }

    [Fact]
    public async Task TwoUsersWithTheSameRightsGetTheirOwnScopeSets()
    {
        // Defect 1 (docs/access.md): the owner rule filters by user id, so the set must not
        // be shared between users whose roles match.
        var provider = new CountingProvider("ContentItemIndex");
        var scopes = new ScopeSetProvider([provider]);
        var twin = Caller("u-editor-2", CallerSide.Admin, ["Editor"], ["ViewContent_Page", "EditContent"]);

        var one = await scopes.GetAsync(Editor, TestContext.Current.CancellationToken);
        var two = await scopes.GetAsync(twin, TestContext.Current.CancellationToken);

        Assert.NotSame(one, two);
        Assert.Equal(2, provider.Builds);
    }

    [Fact]
    public async Task ATargetWithSeveralProvidersTakesTheirConjunction()
    {
        var scopes = new ScopeSetProvider([new CountingProvider("ContentItemIndex"), new FixedProvider("ContentItemIndex", ScopeRule.None)]);

        var set = await scopes.GetAsync(AccessWorld.Admin, TestContext.Current.CancellationToken);

        Assert.Same(ScopeRule.None, set.Require("ContentItemIndex"));
    }

    private sealed class FixedProvider(string target, ScopeRule rule) : IScopeProvider
    {
        public string Target { get; } = target;

        public int Version => 1;

        public Task<ScopeRule> BuildAsync(CallerContext caller, CancellationToken cancellationToken = default) => Task.FromResult(rule);
    }
}
