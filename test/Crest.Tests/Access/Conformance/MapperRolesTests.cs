using Crest.Access;
using Crest.Access.Services;
using Crest.Environment.Extensions.Features;
using static Crest.Tests.Access.Conformance.AccessWorld;

namespace Crest.Tests.Access.Conformance;

/// <summary>
/// The mapper roles and the module priority system (docs/access.md › Mapper roles): an
/// authority replaces every other answer for what it claims, the highest-priority feature's
/// authority wins, two at one priority conflict and deny, nothing widens past an authority, and
/// a module's own mappers are ordered by priority too.
/// </summary>
public class MapperRolesTests
{
    private sealed record Doc(string Id);

    private sealed class Priorities : Dictionary<Type, int>, IProviderPriority
    {
        public int PriorityOf(Type providerType) => TryGetValue(providerType, out var value) ? value : 0;
    }

    /// <summary>Claims documents whose id starts with its prefix, answering with its candidates.</summary>
    private abstract class Claims(string prefix, params string[] answer)
    {
        public bool Asked { get; private set; }

        public ValueTask<IReadOnlyList<PermissionCandidate>> MapAsync(string permission, object resource, CallerContext caller, CancellationToken cancellationToken = default)
        {
            Asked = true;
            return ValueTask.FromResult<IReadOnlyList<PermissionCandidate>>(resource is Doc doc && doc.Id.StartsWith(prefix, StringComparison.Ordinal)
                ? [.. answer.Select(name => name == "*" ? PermissionCandidate.Granted : (PermissionCandidate)name)]
                : null);
        }
    }

    private sealed class CrestAuthority(string prefix, params string[] answer) : Claims(prefix, answer), IAuthoritativeResourcePermissionMapper;

    private sealed class ThirdPartyAuthority(string prefix, params string[] answer) : Claims(prefix, answer), IAuthoritativeResourcePermissionMapper;

    private sealed class ModuleMapper(string prefix, params string[] answer) : Claims(prefix, answer), IResourcePermissionMapper;

    private sealed class OtherModuleMapper(string prefix, params string[] answer) : Claims(prefix, answer), IResourcePermissionMapper;

    private static AccessDecisionService Decision(IProviderPriority priority, params IResourcePermissionMapper[] mappers) =>
        new(new PermissionRegistry(), [], mappers, new RecordingAuditor(), priority);

    private static CallerContext Holding(params string[] permissions) => Caller("u", CallerSide.Admin, permissions: permissions);

    [Fact]
    public async Task AnAuthorityReplacesTheModulesOwnMapperForWhatItClaims()
    {
        var module = new ModuleMapper("doc", "*");
        var decision = Decision(new Priorities(), module, new CrestAuthority("doc", "Nope"));

        Assert.False((await decision.DecideAsync(Holding(), "ViewContent", new Doc("doc-1"), TestContext.Current.CancellationToken)).IsAllowed);
        Assert.False(module.Asked);
    }

    [Fact]
    public async Task AHigherPriorityAuthorityOverridesALowerOneForExactlyWhatItClaims()
    {
        // The third party claims only "doc-special"; Crest's authority keeps the rest.
        var priority = new Priorities { [typeof(ThirdPartyAuthority)] = 10 };
        var decision = Decision(priority, new CrestAuthority("doc", "Nope"), new ThirdPartyAuthority("doc-special", "*"));

        Assert.True((await decision.DecideAsync(Holding(), "ViewContent", new Doc("doc-special"), TestContext.Current.CancellationToken)).IsAllowed);
        Assert.False((await decision.DecideAsync(Holding(), "ViewContent", new Doc("doc-other"), TestContext.Current.CancellationToken)).IsAllowed);
    }

    [Fact]
    public async Task RegistrationOrderNeverDecidesBetweenAuthorities()
    {
        var priority = new Priorities { [typeof(ThirdPartyAuthority)] = 10 };
        var first = Decision(priority, new ThirdPartyAuthority("doc", "*"), new CrestAuthority("doc", "Nope"));
        var second = Decision(priority, new CrestAuthority("doc", "Nope"), new ThirdPartyAuthority("doc", "*"));

        Assert.True((await first.DecideAsync(Holding(), "ViewContent", new Doc("doc-1"), TestContext.Current.CancellationToken)).IsAllowed);
        Assert.True((await second.DecideAsync(Holding(), "ViewContent", new Doc("doc-1"), TestContext.Current.CancellationToken)).IsAllowed);
    }

    [Fact]
    public async Task TwoAuthoritiesClaimingOneResourceAtOnePriorityAreAConflictThatDenies()
    {
        var auditor = new RecordingAuditor();
        var decision = new AccessDecisionService(new PermissionRegistry(), [], [new CrestAuthority("doc", "*"), new ThirdPartyAuthority("doc", "*")], auditor, new Priorities());

        var verdict = await decision.DecideAsync(Holding(), "ViewContent", new Doc("doc-1"), TestContext.Current.CancellationToken);

        Assert.False(verdict.IsAllowed);
        Assert.True(verdict.IsFinal);
        Assert.Contains("same priority", verdict.Reason);
        Assert.Single(auditor.Events);
    }

    [Fact]
    public async Task AuthoritiesAtOnePriorityThatClaimDifferentResourcesDoNotConflict()
    {
        var decision = Decision(new Priorities(), new CrestAuthority("doc-a", "*"), new ThirdPartyAuthority("doc-b", "*"));

        Assert.True((await decision.DecideAsync(Holding(), "ViewContent", new Doc("doc-a1"), TestContext.Current.CancellationToken)).IsAllowed);
        Assert.True((await decision.DecideAsync(Holding(), "ViewContent", new Doc("doc-b1"), TestContext.Current.CancellationToken)).IsAllowed);
    }

    [Fact]
    public async Task NoMapperWidensPastTheAuthority()
    {
        // Whatever a module mapper of any priority would grant, the authority that claims the
        // resource is the answer: the only way past it is a higher-priority authority.
        var module = new ModuleMapper("doc", "*");
        var priority = new Priorities { [typeof(ModuleMapper)] = 100 };
        var decision = Decision(priority, module, new CrestAuthority("doc", "Nope"));

        Assert.False((await decision.DecideAsync(Holding(), "ViewContent", new Doc("doc-1"), TestContext.Current.CancellationToken)).IsAllowed);
        Assert.False(module.Asked);
    }

    [Fact]
    public async Task AmongModuleMappersTheHigherPriorityFeaturesAnswers()
    {
        var priority = new Priorities { [typeof(OtherModuleMapper)] = 5 };
        var decision = Decision(priority, new ModuleMapper("doc", "Nope"), new OtherModuleMapper("doc", "*"));

        Assert.True((await decision.DecideAsync(Holding(), "ViewContent", new Doc("doc-1"), TestContext.Current.CancellationToken)).IsAllowed);
    }

    [Fact]
    public async Task WithoutPrioritiesModuleMappersKeepTheirRegistrationOrder()
    {
        var decision = Decision(new Priorities(), new ModuleMapper("doc", "*"), new OtherModuleMapper("doc", "Nope"));

        Assert.True((await decision.DecideAsync(Holding(), "ViewContent", new Doc("doc-1"), TestContext.Current.CancellationToken)).IsAllowed);
    }

    [Fact]
    public async Task TheSuperUserPassesBeforeAnyMapperOrConflict()
    {
        var decision = Decision(new Priorities(), new CrestAuthority("doc", "Nope"), new ThirdPartyAuthority("doc", "Nope"));

        Assert.True((await decision.DecideAsync(AccessWorld.Admin, "ViewContent", new Doc("doc-1"), TestContext.Current.CancellationToken)).IsAllowed);
    }
}
