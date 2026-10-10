using System.Security.Claims;
using Crest.Environment.Shell;
using Crest.Roles;
using Crest.Security;
using Crest.Security.Permissions;
using Crest.Settings;
using Crest.Users;
using Microsoft.AspNetCore.Identity;

namespace Crest.Access.Services;

/// <summary>
/// Builds the caller for a request from the authenticated identity plus server-held state:
/// roles from the user, permissions from the roles (plus the Anonymous role for everyone
/// and the Authenticated role for signed-in users), the super user, then every
/// contributor (Members: organization, binding roles, class).
/// </summary>
public sealed class CallerContextFactory(
    ShellSettings shellSettings,
    UserManager<IUser> userManager,
    RoleManager<IRole> roleManager,
    ISystemRoleProvider systemRoles,
    ISiteService siteService,
    IPermissionVersion permissionVersion,
    CallerStateCache cache,
    IEnumerable<ICallerRequestResolver> resolvers,
    IEnumerable<ICallerContextContributor> contributors) : ICallerContextFactory
{
    public async Task<CallerContext> CreateAsync(CallerRequest request, CancellationToken cancellationToken = default)
    {
        foreach (var resolver in resolvers)
        {
            request = await resolver.ResolveAsync(request, cancellationToken);
        }

        var principal = request.Principal;
        var userId = principal?.Identity?.IsAuthenticated == true
            ? principal.FindFirstValue(ClaimTypes.NameIdentifier)
            : null;
        var version = await permissionVersion.GetAsync(cancellationToken);
        var key = CallerStateCache.Key(shellSettings.Name, userId, request.Side, request.RequestedOrganizationId);

        if (!cache.TryGet(key, version, out var state))
        {
            var builder = new CallerContextBuilder(request, shellSettings.Name)
            {
                UserId = userId,
                UserName = principal?.Identity?.Name,
                ImpersonatorUserId = request.ImpersonatorUserId,
            };

            await AddRolesAndPermissionsAsync(builder, userId, principal, cancellationToken);

            foreach (var contributor in contributors)
            {
                await contributor.ContributeAsync(builder, cancellationToken);
                if (builder.DenialReason is not null)
                {
                    throw new CallerDeniedException(builder.DenialReason);
                }
            }

            state = new CallerState(
                version,
                builder.UserName,
                builder.IsSuperUser,
                new HashSet<string>(builder.Roles, StringComparer.OrdinalIgnoreCase),
                new HashSet<string>(builder.Permissions, StringComparer.OrdinalIgnoreCase),
                builder.OrganizationId,
                builder.UserClass);
            cache.Set(key, state);
        }

        return new CallerContext
        {
            Tenant = shellSettings.Name,
            Side = request.Side,
            UserId = userId,
            UserName = state.UserName,
            OrganizationId = state.OrganizationId,
            UserClass = state.UserClass,
            ImpersonatorUserId = request.ImpersonatorUserId,
            IsSuperUser = state.IsSuperUser,
            Roles = state.Roles,
            Permissions = state.Permissions,
            PermissionVersion = version,
            Culture = request.Culture,
        };
    }

    public async Task<CallerContext> CreateSystemAsync(string? organizationId = null, CancellationToken cancellationToken = default)
    {
        var version = await permissionVersion.GetAsync(cancellationToken);
        if (organizationId is null)
        {
            // The tenant system actor acts as a tenant admin.
            return new CallerContext
            {
                Tenant = shellSettings.Name,
                Side = CallerSide.System,
                IsSystem = true,
                IsSuperUser = true,
                UserName = "system",
                PermissionVersion = version,
            };
        }

        // The organization system actor acts as that organization's admin: the Members
        // contributor supplies the member-admin roles for the organization.
        var builder = new CallerContextBuilder(new CallerRequest(null, CallerSide.System, organizationId, null), shellSettings.Name)
        {
            UserName = "system",
            OrganizationId = organizationId,
        };
        await AddRolesAndPermissionsAsync(builder, null, null, cancellationToken);
        foreach (var contributor in contributors)
        {
            await contributor.ContributeAsync(builder, cancellationToken);
        }

        return new CallerContext
        {
            Tenant = shellSettings.Name,
            Side = CallerSide.System,
            IsSystem = true,
            OrganizationId = organizationId,
            UserClass = builder.UserClass,
            UserName = builder.UserName,
            Roles = new HashSet<string>(builder.Roles, StringComparer.OrdinalIgnoreCase),
            Permissions = new HashSet<string>(builder.Permissions, StringComparer.OrdinalIgnoreCase),
            PermissionVersion = version,
        };
    }

    private async Task AddRolesAndPermissionsAsync(CallerContextBuilder builder, string? userId, ClaimsPrincipal? principal, CancellationToken cancellationToken)
    {
        var roleNames = new List<string> { PlatformConstants.Roles.Anonymous };

        if (userId is not null)
        {
            roleNames.Add(PlatformConstants.Roles.Authenticated);
            var user = await userManager.FindByIdAsync(userId);
            if (user is not null)
            {
                builder.UserName ??= user.UserName;
                foreach (var roleName in await userManager.GetRolesAsync(user))
                {
                    roleNames.Add(roleName);
                    builder.Roles.Add(roleName);
                }
            }
            else if (principal is not null)
            {
                // An application (a client-credentials token): no user record, the
                // application's roles come with the identity, the permissions from the roles.
                builder.UserClass = CallerClasses.Staff;
                foreach (var roleName in principal.FindAll(ClaimTypes.Role).Concat(principal.FindAll("role")).Select(claim => claim.Value).Distinct(StringComparer.OrdinalIgnoreCase))
                {
                    roleNames.Add(roleName);
                    builder.Roles.Add(roleName);
                }
            }

            var adminRole = systemRoles.GetAdminRole();
            if (builder.Roles.Contains(adminRole.RoleName))
            {
                builder.IsSuperUser = true;
            }
            else
            {
                var site = await siteService.GetSiteSettingsAsync();
                builder.IsSuperUser = string.Equals(userId, site.SuperUser, StringComparison.OrdinalIgnoreCase);
            }
        }

        foreach (var roleName in roleNames.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            var role = await roleManager.FindByNameAsync(roleName);
            if (role is null)
            {
                continue;
            }

            foreach (var claim in await roleManager.GetClaimsAsync(role))
            {
                if (string.Equals(claim.Type, Permission.ClaimType, StringComparison.OrdinalIgnoreCase))
                {
                    builder.Permissions.Add(claim.Value);
                }
            }
        }
    }
}
