using Crest.Access;
using Crest.Access.Services;
using Crest.Security.Permissions;

namespace Crest.Tests.Access;

public class AccessDecisionTests
{
    private static readonly Permission Edit = new("EditContent", "Edit");
    private static readonly Permission View = new("ViewContent", "View", [Edit]);
    private static readonly Permission Manage = new("ManageIt", "Manage");

    private sealed class Permissions : IPermissionService
    {
        private readonly Permission[] _all = [Edit, View, Manage];

        public ValueTask<Permission> FindByNameAsync(string name)
            => ValueTask.FromResult(_all.FirstOrDefault(permission => string.Equals(permission.Name, name, StringComparison.OrdinalIgnoreCase)));

        public ValueTask<IEnumerable<Permission>> GetPermissionsAsync() => ValueTask.FromResult<IEnumerable<Permission>>(_all);
    }

    private sealed class Ceiling(string name) : IAccessCeiling
    {
        public string Deny(CallerContext caller, string permission, object resource = null)
            => caller.UserClass == "member" && permission == name ? $"'{permission}' is never granted to members." : null;
    }

    private sealed class Mapper : IResourcePermissionMapper
    {
        public ValueTask<IReadOnlyList<PermissionCandidate>> MapAsync(string permission, object resource, CallerContext caller, CancellationToken cancellationToken = default)
            => ValueTask.FromResult<IReadOnlyList<PermissionCandidate>>(resource is string type ? [$"{permission}_{type}", permission] : null);
    }

    private static CallerContext Caller(bool superUser = false, string userClass = null, params string[] permissions) => new()
    {
        Tenant = "Default",
        Side = CallerSide.Admin,
        UserId = "u1",
        IsSuperUser = superUser,
        UserClass = userClass,
        Permissions = new HashSet<string>(permissions, StringComparer.OrdinalIgnoreCase),
    };

    private sealed class NoAudit : IAccessAuditor
    {
        public Task RecordAsync(AccessEvent accessEvent, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private static AccessDecisionService Decision(params IAccessCeiling[] ceilings)
        => new(new Permissions(), ceilings, [new Mapper()], new NoAudit());

    [Fact]
    public async Task GrantedPermissionIsAllowed()
    {
        var verdict = await Decision().DecideAsync(Caller(permissions: "ViewContent"), "ViewContent");

        Assert.True(verdict.IsAllowed);
    }

    [Fact]
    public async Task ImpliedPermissionIsAllowed()
    {
        // ViewContent is implied by EditContent: holding Edit grants View.
        var verdict = await Decision().DecideAsync(Caller(permissions: "EditContent"), "ViewContent");

        Assert.True(verdict.IsAllowed);
    }

    [Fact]
    public async Task UnknownPermissionIsDenied()
    {
        var verdict = await Decision().DecideAsync(Caller(permissions: "EditContent"), "DeleteIt");

        Assert.Equal(AccessVerdict.Deny, verdict.Verdict);
    }

    [Fact]
    public async Task SuperUserIsAllowedEverything()
    {
        var verdict = await Decision().DecideAsync(Caller(superUser: true), "ManageIt");

        Assert.True(verdict.IsAllowed);
    }

    [Fact]
    public async Task CeilingDeniesEvenTheSuperUserAndIsFinal()
    {
        var verdict = await Decision(new Ceiling("ManageIt")).DecideAsync(Caller(superUser: true, userClass: "member"), "ManageIt");

        Assert.Equal(AccessVerdict.Deny, verdict.Verdict);
        Assert.True(verdict.IsFinal);
    }

    [Fact]
    public async Task ResourceMappingGrantsThroughTheMappedPermission()
    {
        var verdict = await Decision().DecideAsync(Caller(permissions: "ViewContent_Page"), "ViewContent", "Page");

        Assert.True(verdict.IsAllowed);
    }

    [Fact]
    public async Task ResourceMappingStillAcceptsTheBasePermission()
    {
        var verdict = await Decision().DecideAsync(Caller(permissions: "ViewContent"), "ViewContent", "Page");

        Assert.True(verdict.IsAllowed);
    }
}
