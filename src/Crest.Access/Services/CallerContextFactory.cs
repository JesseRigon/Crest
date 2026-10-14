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
    IEnumerable<ICallerContextContributor> contributors,
    IEnumerable<IDerivedRoleProvider>? derivedRoles = null,
    IUserAccessReader? access = null) : ICallerContextFactory
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

            var isUser = await AddRolesAndPermissionsAsync(builder, userId, principal, cancellationToken);

            foreach (var contributor in contributors)
            {
                await contributor.ContributeAsync(builder, cancellationToken);
                if (builder.DenialReason is not null)
                {
                    throw new CallerDeniedException(builder.DenialReason);
                }
            }

            if (isUser)
            {
                await AddDerivedRolesAsync(builder, userId!, cancellationToken);
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
                UserName = SystemAccounts.DisplayName(SystemAccounts.Tenant),
                PermissionVersion = version,
            };
        }

        // The organization system actor acts as that organization's admin: the Members
        // contributor supplies the member-admin roles for the organization.
        var builder = new CallerContextBuilder(new CallerRequest(null, CallerSide.System, organizationId, null), shellSettings.Name)
        {
            UserName = SystemAccounts.DisplayName(SystemAccounts.Organization(organizationId)),
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

    /// <summary>A user's identity (the cookie, a workflow actor): never read as an application.</summary>
    private static bool IsIdentityCookie(ClaimsPrincipal principal) =>
        principal.Identities.Any(identity => identity.AuthenticationType is { } type && type.StartsWith("Identity.", StringComparison.Ordinal));

    /// <summary>
    /// Business roles (docs/sharing.md § 5, requirements 41-42): computed from the relationship
    /// records by every <see cref="IDerivedRoleProvider"/> for the organization the caller acts
    /// for, after the contributors settled it (on the member side they replace the roles with the
    /// binding's), and added with their roles' permissions. Never stored on the user; the cached
    /// caller is rebuilt when a relationship changes, which bumps the permission version (43).
    /// </summary>
    private async Task AddDerivedRolesAsync(CallerContextBuilder builder, string userId, CancellationToken cancellationToken)
    {
        if (derivedRoles is null)
        {
            return;
        }

        var grants = await DerivedRoles.GrantsAsync(derivedRoles, new DerivedRoleContext(userId, builder.OrganizationId), cancellationToken);
        foreach (var roleName in grants.Select(grant => grant.RoleName).Distinct(StringComparer.OrdinalIgnoreCase))
        {
            if (!builder.Roles.Add(roleName))
            {
                continue;
            }

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

    /// <summary>Adds the roles and permissions; true when the caller is a known, enabled user
    /// (not an application, a deleted or a disabled account).</summary>
    private async Task<bool> AddRolesAndPermissionsAsync(CallerContextBuilder builder, string? userId, ClaimsPrincipal? principal, CancellationToken cancellationToken)
    {
        var roleNames = new List<string> { PlatformConstants.Roles.Anonymous };
        var isUser = false;

        if (userId is not null)
        {
            roleNames.Add(PlatformConstants.Roles.Authenticated);
            var user = await userManager.FindByIdAsync(userId);
            var exists = user is not null;
            if (user is Crest.Users.Models.User { IsEnabled: false })
            {
                // A disabled account keeps its session until the stamp check, and a workflow
                // run keeps its actor: neither keeps the account's rights.
                user = null;
            }

            if (user is not null)
            {
                isUser = true;
                builder.UserName ??= user.UserName;

                // The tenant-reach projection (docs/permissions.md › Projection): the user's
                // roles from their groups and assignments, and the permissions those resolve to
                // with the overrides applied. The member side replaces it with the acting
                // organization's (the Members contributor).
                if (access is not null && await access.ReadAsync(userId, AccessReach.Tenant, cancellationToken) is { } projected)
                {
                    builder.Roles.UnionWith(projected.Roles);
                    builder.Permissions.UnionWith(projected.PermissionNames);
                }
                else
                {
                    foreach (var roleName in await userManager.GetRolesAsync(user))
                    {
                        roleNames.Add(roleName);
                        builder.Roles.Add(roleName);
                    }
                }
            }
            else if (!exists && principal is not null && !IsIdentityCookie(principal))
            {
                // An application (a client-credentials token, a remote deployment client): no
                // user record, the application's roles come with the identity, the permissions
                // from the roles (and from contributors, for a credential without roles). The
                // identity cookie never qualifies: a cookie with no user behind it belongs to a
                // user deleted since sign-in, and the roles it lists are not to be trusted.
                builder.UserClass = CallerClasses.Application;
                foreach (var roleName in principal.FindAll(ClaimTypes.Role).Concat(principal.FindAll("role")).Select(claim => claim.Value).Distinct(StringComparer.OrdinalIgnoreCase))
                {
                    roleNames.Add(roleName);
                    builder.Roles.Add(roleName);
                }
            }

            // A deleted or disabled user's cookie or workflow actor is neither: it keeps the
            // Authenticated role until the stamp check signs it out, and is never the super user.
            var known = user is not null || builder.UserClass == CallerClasses.Application;
            if (known && builder.Roles.Contains(systemRoles.GetAdminRole().RoleName))
            {
                builder.IsSuperUser = true;
            }
            else if (known)
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

        return isUser;
    }
}
