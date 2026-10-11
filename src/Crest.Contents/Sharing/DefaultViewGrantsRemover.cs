using Crest.Environment.Extensions.Features;
using Crest.Environment.Shell;
using Crest.Security;
using Crest.Security.Permissions;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;

namespace Crest.Contents.Sharing;

/// <summary>
/// Requirement 84: when the Sharing feature is enabled, the Anonymous and Authenticated roles
/// lose their default content view grants (<c>ViewContent</c>, <c>ViewOwnContent</c> and the
/// per-type <c>View_{Type}</c>, <c>ViewOwn_{Type}</c>). With sharing on, an item's view is
/// decided by sharing anyway (default deny: public needs an Anyone grant, the tenant a Tenant
/// grant); the role grants would still answer the checks asked without an item, and would make
/// every published item public again the day sharing is turned off. Turning sharing off does
/// not restore them: re-grant them on the Roles screen, or by recipe, where that is meant.
/// </summary>
public sealed class DefaultViewGrantsRemover(IServiceProvider services, ILogger<DefaultViewGrantsRemover> logger) : FeatureEventHandler
{
    public const string SharingFeatureId = "Crest.Sharing";

    public static readonly string[] Roles = [PlatformConstants.Roles.Anonymous, PlatformConstants.Roles.Authenticated];

    public override async Task EnabledAsync(IFeatureInfo feature)
    {
        // Roles are a module of their own: without it there are no role grants to remove.
        if (feature.Id != SharingFeatureId || services.GetService(typeof(RoleManager<IRole>)) is not RoleManager<IRole> roles)
        {
            return;
        }

        foreach (var name in Roles)
        {
            if (await roles.FindByNameAsync(name) is not Role role)
            {
                continue;
            }

            var removed = role.RoleClaims.RemoveAll(claim => claim.ClaimType == Permission.ClaimType && IsViewGrant(claim.ClaimValue));
            if (removed > 0)
            {
                await roles.UpdateAsync(role);
                logger.LogInformation("Sharing is on: removed {Count} default content view grant(s) from the {Role} role (requirement 84).", removed, name);
            }
        }
    }

    public static bool IsViewGrant(string permission) =>
        permission is "ViewContent" or "ViewOwnContent" ||
        (permission.StartsWith("View_", StringComparison.Ordinal) && permission.Length > 5) ||
        (permission.StartsWith("ViewOwn_", StringComparison.Ordinal) && permission.Length > 8);
}
