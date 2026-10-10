using Crest.Access;
using Crest.Entities;
using Crest.Members.Constants;
using Crest.Members.Indexes;
using Crest.Members.Models;
using Crest.Security;
using Crest.Security.Permissions;
using Crest.Users;
using Crest.Users.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using YesSql;

namespace Crest.Members.Services;

/// <summary>
/// The registry of permissions that are NEVER valid for member-class callers
/// (design: docs/members.md §E). Name-based on purpose: Permission equality is
/// reference-based upstream, and dynamic permissions exist only as formatted names.
/// Modules contribute their staff-only permissions via
/// <c>services.Configure&lt;MemberPermissionCeilingOptions&gt;(...)</c> alongside their
/// normal IPermissionProvider. Prefixes cover the dynamic expansions
/// (ManageUsersInRole_{role}, …) so the ceiling stays CLOSED under derived permissions.
/// </summary>
public class MemberPermissionCeilingOptions
{
    private readonly HashSet<string> _names = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<string> _namePrefixes = [];

    public MemberPermissionCeilingOptions Ceiling(params string[] permissionNames)
    {
        foreach (var name in permissionNames)
        {
            _names.Add(name);
        }

        return this;
    }

    public MemberPermissionCeilingOptions CeilingPrefix(params string[] namePrefixes)
    {
        _namePrefixes.AddRange(namePrefixes);
        return this;
    }

    public bool IsCeilinged(string permissionName)
    {
        if (_names.Contains(permissionName))
        {
            return true;
        }

        foreach (var prefix in _namePrefixes)
        {
            if (permissionName.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }
}

/// <summary>
/// The class ceiling as the one decision's deny-only rule: a member-class caller never holds
/// a ceilinged permission, whatever any grant says (a final denial).
/// </summary>
public sealed class MemberClassCeiling(IOptions<MemberPermissionCeilingOptions> options) : IAccessCeiling
{
    public string? Deny(CallerContext caller, string permission)
    {
        if (caller.UserClass != UserClasses.Member)
        {
            return null;
        }

        return options.Value.IsCeilinged(permission)
            ? $"Permission '{permission}' can never be granted to a member-class user."
            : null;
    }
}

/// <summary>
/// What Members knows about a caller (design: docs/members.md §D, reworked onto the one
/// caller): the user class; on the member side the organization the request names (the
/// <c>X-Org</c> header, else the session's active organization), validated against the
/// user's bindings, with ONLY that binding's roles and permissions (a dual user's staff
/// grants never reach the portal); the impersonator; and for an organization system actor
/// the organization's member-admin roles.
/// </summary>
public sealed class MemberCallerContributor(
    UserManager<IUser> userManager,
    RoleManager<IRole> roleManager,
    YesSql.ISession session) : ICallerContextContributor
{
    public async Task ContributeAsync(CallerContextBuilder builder, CancellationToken cancellationToken = default)
    {
        if (builder.Request.Side == CallerSide.System)
        {
            if (builder.OrganizationId is not null)
            {
                await ActAsOrganizationAdminAsync(builder, builder.OrganizationId);
            }

            return;
        }

        if (builder.UserId is null)
        {
            return;
        }

        var user = await userManager.FindByIdAsync(builder.UserId) as User;
        if (user is null)
        {
            return;
        }

        builder.UserClass = user.TryGet<CrestUserClass>(out var userClass) ? userClass.Class : UserClasses.Staff;

        if (builder.Request.Side != CallerSide.Member)
        {
            if (builder.UserClass == UserClasses.Member)
            {
                // A member acts only on the member side. The admin is refused outright; the
                // public site sees the member as a signed-in visitor with no tenant rights.
                if (builder.Request.Side == CallerSide.Admin)
                {
                    builder.DenialReason = "A member-class user cannot act on the admin side.";
                    return;
                }

                await ReplaceRolesAsync(builder, []);
            }

            return;
        }

        // The organization the request names, or the session's last choice (filled in by the
        // request resolver); never a default.
        var organizationId = builder.Request.RequestedOrganizationId;
        var member = user.TryGet<CrestMemberInfo>(out var info) ? info : null;
        var binding = organizationId is null
            ? null
            : member?.Bindings.FirstOrDefault(candidate => string.Equals(candidate.OrganizationId, organizationId, StringComparison.Ordinal));

        if (binding is null)
        {
            // A member-side request names an organization the user holds no binding to, or
            // names none at all: denied, never defaulted to a side or an organization.
            builder.DenialReason = organizationId is null
                ? "A member-side request must name an organization."
                : $"The user holds no binding to organization '{organizationId}'.";
            return;
        }

        builder.OrganizationId = organizationId;
        await ReplaceRolesAsync(builder, binding.Roles);
    }

    private async Task ActAsOrganizationAdminAsync(CallerContextBuilder builder, string organizationId)
    {
        builder.UserClass = UserClasses.Member;
        var admins = await session
            .Query<User, MemberOrgBindingIndex>(index => index.OrganizationId == organizationId && index.IsMemberAdmin)
            .ListAsync();
        var roles = admins
            .SelectMany(admin => admin.TryGet<CrestMemberInfo>(out var info) ? info.Bindings : [])
            .Where(binding => string.Equals(binding.OrganizationId, organizationId, StringComparison.Ordinal))
            .SelectMany(binding => binding.Roles)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        await ReplaceRolesAsync(builder, roles);
    }

    /// <summary>On the member side the caller holds the binding's roles and nothing else; the
    /// Anonymous and Authenticated roles stay, since every caller has them.</summary>
    private async Task ReplaceRolesAsync(CallerContextBuilder builder, IEnumerable<string> roleNames)
    {
        builder.Roles.Clear();
        builder.Permissions.Clear();
        builder.IsSuperUser = false;

        var names = new List<string> { PlatformConstants.Roles.Anonymous, PlatformConstants.Roles.Authenticated };
        names.AddRange(roleNames);
        foreach (var roleName in names.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            var role = await roleManager.FindByNameAsync(roleName);
            if (role is null)
            {
                continue;
            }

            if (!string.Equals(roleName, PlatformConstants.Roles.Anonymous, StringComparison.OrdinalIgnoreCase)
                && !string.Equals(roleName, PlatformConstants.Roles.Authenticated, StringComparison.OrdinalIgnoreCase))
            {
                builder.Roles.Add(roleName);
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

/// <summary>
/// Completes a request before the caller is built: on the member side an organization the
/// request does not name comes from the session's last choice, and the impersonator comes
/// from the session. Both are request-level facts, read fresh every time and never cached.
/// </summary>
public sealed class MemberCallerRequestResolver(IHttpContextAccessor httpContextAccessor, MemberSessionService sessionService) : ICallerRequestResolver
{
    public async Task<CallerRequest> ResolveAsync(CallerRequest request, CancellationToken cancellationToken = default)
    {
        var httpContext = httpContextAccessor.HttpContext;
        if (httpContext is null || request.Principal?.Identity?.IsAuthenticated != true)
        {
            return request;
        }

        var organizationId = request.RequestedOrganizationId;
        if (organizationId is null && request.Side == CallerSide.Member)
        {
            organizationId = await sessionService.GetActiveOrganizationAsync(httpContext);
        }

        var impersonator = await sessionService.GetImpersonatorAsync(httpContext);
        return request with { RequestedOrganizationId = organizationId, ImpersonatorUserId = impersonator };
    }
}
