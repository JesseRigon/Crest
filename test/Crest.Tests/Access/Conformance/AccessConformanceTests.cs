using System.Security.Claims;
using Crest.Access;
using Crest.Access.Services;
using Crest.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using static Crest.Tests.Access.Conformance.AccessWorld;

namespace Crest.Tests.Access.Conformance;

/// <summary>
/// Every case of <see cref="AccessWorld"/> against every pipeline that answers "may this
/// caller do this": the decision itself and the platform's <see cref="IAuthorizationService"/>
/// (MVC attributes, Razor, every module still calling <c>AuthorizeAsync</c>). The workflow
/// activity gate runs the same table in Crest.Workflows.Tests. A pipeline that differs from
/// the table, or from another pipeline, fails.
/// </summary>
public class AccessConformanceTests
{
    [Theory]
    [MemberData(nameof(AccessWorld.Theory), MemberType = typeof(AccessWorld))]
    public async Task TheDecisionAnswersPerTheTable(Case @case)
    {
        var verdict = await Decision().DecideAsync(@case.Caller, @case.Permission, @case.Resource, TestContext.Current.CancellationToken);

        Assert.Equal(@case.Expected, Classify(verdict));
    }

    [Theory]
    [MemberData(nameof(AccessWorld.Theory), MemberType = typeof(AccessWorld))]
    public async Task TheAuthorizationServiceAnswersAsTheDecision(Case @case)
    {
        var services = AuthorizationServices(@case.Caller);
        var authorization = services.GetRequiredService<IAuthorizationService>();
        var permission = await new PermissionRegistry().FindByNameAsync(@case.Permission) ?? new global::Crest.Security.Permissions.Permission(@case.Permission);

        var result = await authorization.AuthorizeAsync(PrincipalOf(@case.Caller), @case.Resource, new PermissionRequirement(permission));

        var expectedAllowed = @case.Expected == Expect.Allow;
        Assert.Equal(expectedAllowed, result.Succeeded);
        if (@case.Expected == Expect.DenyFinal)
        {
            // A ceiling fails the requirement outright, so no other handler can succeed it.
            Assert.True(result.Failure?.FailCalled);
        }
    }

    [Fact]
    public async Task EveryDenialIsAuditedOnceAndNoAllowIs()
    {
        var auditor = new RecordingAuditor();
        var decision = Decision(auditor);
        var denials = 0;

        foreach (var @case in Cases())
        {
            var verdict = await decision.DecideAsync(@case.Caller, @case.Permission, @case.Resource, TestContext.Current.CancellationToken);
            if (!verdict.IsAllowed)
            {
                denials++;
            }
        }

        Assert.Equal(denials, auditor.Events.Count);
        Assert.All(auditor.Events, recorded =>
        {
            Assert.Equal(AccessEventKind.Decision, recorded.Kind);
            Assert.NotEqual(AccessVerdict.Allow, recorded.Verdict);
            Assert.False(string.IsNullOrEmpty(recorded.Reason));
        });
    }

    [Fact]
    public async Task TheAuditNamesTheResourceAndTheCaller()
    {
        var auditor = new RecordingAuditor();

        await Decision(auditor).DecideAsync(Anonymous, "ViewContent", DraftPost, TestContext.Current.CancellationToken);

        var recorded = Assert.Single(auditor.Events);
        Assert.Equal("ViewContent", recorded.Operation);
        Assert.Equal(DraftPost.ToString(), recorded.Resource);
        Assert.Same(Anonymous, recorded.Caller);
    }

    [Fact]
    public async Task TheSameQuestionTwiceGetsTheSameAnswer()
    {
        // The decision holds no state between calls: no cached grant can outlive a caller.
        var decision = Decision();
        foreach (var @case in Cases())
        {
            var first = await decision.DecideAsync(@case.Caller, @case.Permission, @case.Resource, TestContext.Current.CancellationToken);
            var second = await decision.DecideAsync(@case.Caller, @case.Permission, @case.Resource, TestContext.Current.CancellationToken);

            Assert.Equal(Classify(first), Classify(second));
        }
    }

    [Fact]
    public async Task ACallerWithFewerRightsNeverGetsMore()
    {
        // Monotonicity: dropping a permission from a caller can turn an allow into a deny,
        // never a deny into an allow.
        var decision = Decision();
        foreach (var @case in Cases().Where(c => c.Caller.Permissions.Count > 0))
        {
            var full = await decision.DecideAsync(@case.Caller, @case.Permission, @case.Resource, TestContext.Current.CancellationToken);
            foreach (var dropped in @case.Caller.Permissions)
            {
                var narrowed = Caller(
                    @case.Caller.UserId,
                    @case.Caller.Side,
                    [.. @case.Caller.Roles],
                    [.. @case.Caller.Permissions.Where(p => p != dropped)],
                    @case.Caller.IsSuperUser,
                    @case.Caller.UserClass,
                    @case.Caller.OrganizationId);
                var verdict = await decision.DecideAsync(narrowed, @case.Permission, @case.Resource, TestContext.Current.CancellationToken);

                Assert.False(verdict.IsAllowed && !full.IsAllowed, $"{@case.Name}: dropping {dropped} granted more.");
            }
        }
    }

    private static Expect Classify(AccessDecision verdict) =>
        verdict.IsAllowed ? Expect.Allow : verdict.IsFinal ? Expect.DenyFinal : Expect.Deny;

    /// <summary>The principal the gate authenticated for this caller: identity only.</summary>
    private static ClaimsPrincipal PrincipalOf(CallerContext caller) =>
        caller.UserId is null
            ? new ClaimsPrincipal(new ClaimsIdentity())
            : new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, caller.UserId), new Claim(ClaimTypes.Name, caller.UserName ?? caller.UserId)], "Identity.Application"));

    private sealed class Accessor : ICallerContextAccessor
    {
        public CallerContext Current { get; set; }
    }

    /// <summary>The authorization service as the platform wires it, with the gate's caller set.</summary>
    private static IServiceProvider AuthorizationServices(CallerContext caller)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddAuthorizationCore();
        services.AddSingleton<ICallerContextAccessor>(new Accessor { Current = caller });
        var decision = Decision();
        services.AddSingleton(decision);
        services.AddSingleton<IAccessDecision>(decision);
        services.AddSingleton(Mock.Of<ICallerContextFactory>(MockBehavior.Strict));
        services.AddSingleton(Mock.Of<IHttpContextAccessor>());
        services.AddScoped<IAuthorizationHandler, AccessAuthorizationHandler>();
        return services.BuildServiceProvider().CreateScope().ServiceProvider;
    }
}
