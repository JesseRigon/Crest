using System.Security.Claims;
using Grove.Access;
using Grove.Access.Services;
using Grove.Security.Permissions;

namespace Grove.Tests.Access;

/// <summary>
/// The one decision's implied-by answer for a permission instance, for tests that check a
/// module's permission chains: the caller holds exactly <c>granted</c>, and the instance's
/// chain decides (no registry, no mappers, no ceilings).
/// </summary>
public static class ImpliedPermissions
{
    private sealed class NoRegistry : IPermissionService
    {
        public ValueTask<Permission> FindByNameAsync(string name) => ValueTask.FromResult<Permission>(null);

        public ValueTask<IEnumerable<Permission>> GetPermissionsAsync() => ValueTask.FromResult(Enumerable.Empty<Permission>());
    }

    private sealed class NoAudit : IAccessAuditor
    {
        public Task RecordAsync(AccessEvent accessEvent, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    public static bool IsGranted(Permission required, IEnumerable<string> granted)
    {
        var caller = new CallerContext
        {
            Tenant = "test",
            Side = CallerSide.Admin,
            UserId = "u-1",
            Permissions = new HashSet<string>(granted, StringComparer.OrdinalIgnoreCase),
        };

        return new AccessDecisionService(new NoRegistry(), [], [], new NoAudit()).DecideAsync(caller, required).GetAwaiter().GetResult().IsAllowed;
    }

    /// <summary>The permission claims of a principal or claim list, as the role's grants.</summary>
    public static bool IsGranted(Permission required, IEnumerable<Claim> claims) =>
        IsGranted(required, (claims ?? []).Where(claim => string.Equals(claim.Type, Permission.ClaimType, StringComparison.OrdinalIgnoreCase)).Select(claim => claim.Value));
}
