#nullable enable

using Grove.Access;
using Grove.Access.Roles;
using Grove.Security;
using Grove.Security.Permissions;
using Grove.Security.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Grove.Api;
using Grove.Validation;

namespace Grove.Access.Api;

public sealed class AccessModelApi : ApiContributor
{
    public override string Name => "AccessModel";

    public override string FeatureId => "Grove.Access";

    public override void Map(IEndpointRouteBuilder group)
    {
        var routes = group.MapGroup("");
        routes.MapGet("permission-catalog", (HttpContext __http, [FromQuery] string? organizationId = null) => ApiHandlers.For<AccessModelHandlers>(__http).CatalogAsync(organizationId));
        routes.MapGet("permission-sets", ApiHandlers.Handle<AccessModelHandlers>(h => h.ListSetsAsync()));
        routes.MapPut("permission-sets/{name}", (HttpContext __http, [FromRoute] string name, [FromBody] PermissionSetInfo write) => ApiHandlers.For<AccessModelHandlers>(__http).SaveSetAsync(name, write));
        routes.MapDelete("permission-sets/{name}", (HttpContext __http, [FromRoute] string name) => ApiHandlers.For<AccessModelHandlers>(__http).DeleteSetAsync(name));
        routes.MapGet("roles/{name}", (HttpContext __http, [FromRoute] string name) => ApiHandlers.For<AccessModelHandlers>(__http).GetRoleAsync(name));
        routes.MapPost("roles", (HttpContext __http, [FromBody] RoleWrite write) => ApiHandlers.For<AccessModelHandlers>(__http).CreateRoleAsync(write));
        routes.MapPut("roles/{name}", (HttpContext __http, [FromRoute] string name, [FromBody] RoleWrite write) => ApiHandlers.For<AccessModelHandlers>(__http).UpdateRoleAsync(name, write));
        routes.MapDelete("roles/{name}", (HttpContext __http, [FromRoute] string name) => ApiHandlers.For<AccessModelHandlers>(__http).DeleteRoleAsync(name));
        routes.MapGet("access/groups", (HttpContext __http, [FromQuery] string? organizationId = null) => ApiHandlers.For<AccessModelHandlers>(__http).ListGroupsAsync(organizationId));
        routes.MapPost("access/groups", (HttpContext __http, [FromQuery] string? organizationId, [FromBody] AccessGroupWrite write) => ApiHandlers.For<AccessModelHandlers>(__http).CreateGroupAsync(organizationId, write));
        routes.MapPut("access/groups/{groupId}", (HttpContext __http, [FromRoute] string groupId, [FromBody] AccessGroupWrite write) => ApiHandlers.For<AccessModelHandlers>(__http).UpdateGroupAsync(groupId, write));
        routes.MapDelete("access/groups/{groupId}", (HttpContext __http, [FromRoute] string groupId) => ApiHandlers.For<AccessModelHandlers>(__http).DeleteGroupAsync(groupId));
        routes.MapPost("access/groups/{groupId}/users", (HttpContext __http, [FromRoute] string groupId, [FromBody] GroupUserRequest request) => ApiHandlers.For<AccessModelHandlers>(__http).AddGroupUserAsync(groupId, request));
        routes.MapDelete("access/groups/{groupId}/users/{userId}", (HttpContext __http, [FromRoute] string groupId, [FromRoute] string userId) => ApiHandlers.For<AccessModelHandlers>(__http).RemoveGroupUserAsync(groupId, userId));
        routes.MapGet("access/managed-users", (HttpContext __http, [FromQuery] string? organizationId = null) => ApiHandlers.For<AccessModelHandlers>(__http).ManagedUsersAsync(organizationId));
        routes.MapGet("access/users/{userId}/overrides", (HttpContext __http, [FromRoute] string userId, [FromQuery] string? organizationId = null) => ApiHandlers.For<AccessModelHandlers>(__http).GetOverridesAsync(userId, organizationId));
        routes.MapPut("access/users/{userId}/overrides", (HttpContext __http, [FromRoute] string userId, [FromQuery] string? organizationId, [FromBody] UserOverridesModel model) => ApiHandlers.For<AccessModelHandlers>(__http).SetOverridesAsync(userId, organizationId, model));
        routes.MapGet("access/users/{userId}", (HttpContext __http, [FromRoute] string userId, [FromQuery] string? organizationId = null) => ApiHandlers.For<AccessModelHandlers>(__http).GetUserAccessAsync(userId, organizationId));
        routes.MapGet("access/organization-defaults", ApiHandlers.Handle<AccessModelHandlers>(h => h.GetDefaultsAsync()));
        routes.MapPut("access/organization-defaults", (HttpContext __http, [FromBody] OrganizationAccessDefaults model) => ApiHandlers.For<AccessModelHandlers>(__http).SetDefaultsAsync(model));
        routes.MapGet("access/assignable-roles", (HttpContext __http, [FromQuery] string organizationId) => ApiHandlers.For<AccessModelHandlers>(__http).AssignableRolesAsync(organizationId));
    }
}


public sealed record PermissionInfo(string Name, string? Description, string? Category, bool IsSecurityCritical);

public sealed record PermissionSetInfo(string Name, string? Description, IReadOnlyList<string> Permissions);

public sealed record RoleDetail(string Name, string? Description, IReadOnlyList<string> Sets, IReadOnlyList<string> Permissions, bool IsAdmin, bool IsSystem);

public sealed record RoleWrite([property: NotEmpty] string Name, string? Description, IReadOnlyList<string>? Sets, IReadOnlyList<string>? Permissions);

public sealed record GroupUserRequest(string UserId);

public sealed record UserOverridesModel(IReadOnlyList<UserAccessOverride> Overrides);

public sealed record ManagedUser(string Id, string Label);

/// <summary>
/// The editors' API for the permission model (docs/permissions.md › Editors, Who manages the
/// permission model): the permission catalog, permission sets, roles with their sets and loose
/// permissions, groups at a reach, per-user overrides and the organization defaults. Each
/// operation takes its CRUD permission (the "Manage permissions" and "Manage groups" sets),
/// asked against the organization at an organization's reach, so staff hold it for every
/// organization and an organization's admin for their own. Writing a user's overrides or group
/// membership is further bounded by the hierarchy (<see cref="IUserManagementReach"/>): only
/// the users under the caller there. An organization's groups hold only roles from the
/// tenant's assignable list; owner-locked permissions are never granted by override.
/// </summary>
public sealed class AccessModelHandlers(
    IAccessDecision decision,
    ICallerContextAccessor callers,
    IPermissionService permissions,
    IPermissionSetService sets,
    RoleManager<IRole> roleManager,
    Grove.Security.Services.IRoleService roleService,
    IAccessGroupService groups,
    IUserAccessOverrideService overrides,
    IOrganizationAccessDefaultsService defaults,
    IUserAccessReader access,
    UserManager<Grove.Access.Users.IUser> userManager,
    IServiceProvider services) : ApiHandlers
{
    private IUserManagementReach? Hierarchy => services.GetService(typeof(IUserManagementReach)) as IUserManagementReach;

    // --- Catalog and sets (tenant schema) ---

    public async Task<IResult> CatalogAsync([FromQuery] string? organizationId = null)
    {
        if (!await CanAsync(AccessPermissions.ReadPermissionCatalog, organizationId))
        {
            return Forbid();
        }

        return Ok((await permissions.GetPermissionsAsync())
            .Select(permission => new PermissionInfo(permission.Name, permission.Description, permission.Category, permission.IsSecurityCritical))
            .DistinctBy(permission => permission.Name)
            .OrderBy(permission => permission.Category).ThenBy(permission => permission.Name)
            .ToArray());
    }

    public async Task<IResult> ListSetsAsync()
    {
        if (!await CanAsync(AccessPermissions.ReadPermissionSets, null))
        {
            return Forbid();
        }

        return Ok((await sets.ListAsync(HttpContext.RequestAborted)).Select(ToInfo).ToArray());
    }

    public async Task<IResult> SaveSetAsync(string name, PermissionSetInfo write)
    {
        if (!await CanAsync((await sets.FindAsync(name, HttpContext.RequestAborted) is null ? AccessPermissions.CreatePermissionSets : AccessPermissions.UpdatePermissionSets), null))
        {
            return Forbid();
        }

        if (string.IsNullOrWhiteSpace(name))
        {
            return BadRequest(new { error = "A permission set needs a name." });
        }

        await sets.SaveAsync(new PermissionSet { Name = name, Description = write.Description, Permissions = [.. write.Permissions ?? []] }, HttpContext.RequestAborted);
        return Ok(ToInfo((await sets.FindAsync(name, HttpContext.RequestAborted))!));
    }

    public async Task<IResult> DeleteSetAsync(string name)
    {
        if (!await CanAsync(AccessPermissions.DeletePermissionSets, null))
        {
            return Forbid();
        }

        return await sets.DeleteAsync(name, HttpContext.RequestAborted) ? NoContent() : NotFound();
    }

    // --- Roles: sets and loose permissions ---

    public async Task<IResult> GetRoleAsync(string name)
    {
        if (!await CanAsync(AccessPermissions.ReadRoles, null))
        {
            return Forbid();
        }

        return await roleManager.FindByNameAsync(name) is Role role ? Ok(await ToDetailAsync(role)) : NotFound();
    }

    public async Task<IResult> CreateRoleAsync(RoleWrite write)
    {
        if (!await CanAsync(AccessPermissions.CreateRoles, null))
        {
            return Forbid();
        }

        if (await roleManager.FindByNameAsync(write.Name) is not null)
        {
            return BadRequest(new { error = "The role needs a name no other role has." });
        }

        var role = new Role { RoleName = write.Name.Trim() };
        Apply(role, write);
        var result = await roleManager.CreateAsync(role);
        return result.Succeeded ? Ok(await ToDetailAsync(role)) : BadRequest(new { error = string.Join(" ", result.Errors.Select(error => error.Description)) });
    }

    public async Task<IResult> UpdateRoleAsync(string name, RoleWrite write)
    {
        if (!await CanAsync(AccessPermissions.UpdateRoles, null))
        {
            return Forbid();
        }

        if (await roleManager.FindByNameAsync(name) is not Role role)
        {
            return NotFound();
        }

        Apply(role, write);
        var result = await roleManager.UpdateAsync(role);
        return result.Succeeded ? Ok(await ToDetailAsync(role)) : BadRequest(new { error = string.Join(" ", result.Errors.Select(error => error.Description)) });
    }

    public async Task<IResult> DeleteRoleAsync(string name)
    {
        if (!await CanAsync(AccessPermissions.DeleteRoles, null))
        {
            return Forbid();
        }

        if (await roleManager.FindByNameAsync(name) is not { } role)
        {
            return NotFound();
        }

        var result = await roleManager.DeleteAsync(role);
        return result.Succeeded ? NoContent() : BadRequest(new { error = string.Join(" ", result.Errors.Select(error => error.Description)) });
    }

    // --- Groups at a reach ---

    public async Task<IResult> ListGroupsAsync([FromQuery] string? organizationId = null)
    {
        if (!await CanAsync(AccessPermissions.ReadGroups, organizationId))
        {
            return Forbid();
        }

        return Ok(await groups.ListAsync(Reach(organizationId), HttpContext.RequestAborted));
    }

    public async Task<IResult> CreateGroupAsync([FromQuery] string? organizationId, AccessGroupWrite write)
    {
        if (!await CanAsync(AccessPermissions.CreateGroups, organizationId))
        {
            return Forbid();
        }

        if (await RoleRefusalAsync(organizationId, write.Roles) is { } refusal)
        {
            return BadRequest(new { error = refusal });
        }

        return string.IsNullOrWhiteSpace(write.Name)
            ? BadRequest(new { error = "A group needs a name." })
            : Ok(await groups.CreateAsync(Reach(organizationId), write, HttpContext.RequestAborted));
    }

    public async Task<IResult> UpdateGroupAsync(string groupId, AccessGroupWrite write)
    {
        if (await groups.GetAsync(groupId, HttpContext.RequestAborted) is not { } group)
        {
            return NotFound();
        }

        if (!await CanAsync(AccessPermissions.UpdateGroups, group.Reach.OrganizationId))
        {
            return Forbid();
        }

        if (await RoleRefusalAsync(group.Reach.OrganizationId, write.Roles) is { } refusal)
        {
            return BadRequest(new { error = refusal });
        }

        try
        {
            return Ok(await groups.UpdateAsync(groupId, write, HttpContext.RequestAborted));
        }
        catch (OwnerProtectionException protection)
        {
            return BadRequest(new { error = protection.Message });
        }
    }

    public async Task<IResult> DeleteGroupAsync(string groupId)
    {
        if (await groups.GetAsync(groupId, HttpContext.RequestAborted) is not { } group)
        {
            return NotFound();
        }

        if (!await CanAsync(AccessPermissions.DeleteGroups, group.Reach.OrganizationId))
        {
            return Forbid();
        }

        try
        {
            await groups.DeleteAsync(groupId, HttpContext.RequestAborted);
        }
        catch (OwnerProtectionException protection)
        {
            return BadRequest(new { error = protection.Message });
        }

        return NoContent();
    }

    public async Task<IResult> AddGroupUserAsync(string groupId, GroupUserRequest request)
    {
        if (await groups.GetAsync(groupId, HttpContext.RequestAborted) is not { } group)
        {
            return NotFound();
        }

        // An organization's group takes that organization's members only (the root guard), and
        // the caller manages only the users under them (the hierarchy).
        if (!await CanAsync(AccessPermissions.CreateGroupMembership, group.Reach.OrganizationId, group.Reach.IsTenant ? null : new OrganizationMemberResource(group.Reach.OrganizationId!, request.UserId))
            || !await ManagesAsync(group.Reach, request.UserId))
        {
            return Forbid();
        }

        await groups.AddUserAsync(groupId, request.UserId, HttpContext.RequestAborted);
        return NoContent();
    }

    public async Task<IResult> RemoveGroupUserAsync(string groupId, string userId)
    {
        if (await groups.GetAsync(groupId, HttpContext.RequestAborted) is not { } group)
        {
            return NotFound();
        }

        if (!await CanAsync(AccessPermissions.DeleteGroupMembership, group.Reach.OrganizationId) || !await ManagesAsync(group.Reach, userId))
        {
            return Forbid();
        }

        try
        {
            await groups.RemoveUserAsync(groupId, userId, HttpContext.RequestAborted);
        }
        catch (OwnerProtectionException protection)
        {
            return BadRequest(new { error = protection.Message });
        }

        return NoContent();
    }

    /// <summary>The users the editors list at a reach: the ones the caller manages there.</summary>
    public async Task<IResult> ManagedUsersAsync([FromQuery] string? organizationId = null)
    {
        if (!await CanAsync(AccessPermissions.ReadGroupMembership, organizationId) && !await CanAsync(AccessPermissions.ReadUserOverrides, organizationId))
        {
            return Forbid();
        }

        var managed = callers.Current is { } caller && Hierarchy is { } hierarchy
            ? await hierarchy.ManagedUsersAsync(caller, Reach(organizationId), HttpContext.RequestAborted) ?? new HashSet<string>()
            : new HashSet<string>();
        var users = new List<ManagedUser>();
        foreach (var userId in managed)
        {
            if (await userManager.FindByIdAsync(userId) is { } user)
            {
                users.Add(new ManagedUser(userId, user.UserName ?? userId));
            }
        }

        return Ok(users.OrderBy(user => user.Label, StringComparer.OrdinalIgnoreCase).ToArray());
    }

    // --- Per-user overrides and the resolved view ---

    public async Task<IResult> GetOverridesAsync(string userId, [FromQuery] string? organizationId = null)
    {
        if (!await CanAsync(AccessPermissions.ReadUserOverrides, organizationId) || !await ManagesAsync(Reach(organizationId), userId))
        {
            return Forbid();
        }

        return Ok(new UserOverridesModel(await overrides.GetAsync(userId, Reach(organizationId), HttpContext.RequestAborted)));
    }

    public async Task<IResult> SetOverridesAsync(string userId, [FromQuery] string? organizationId, UserOverridesModel model)
    {
        var existing = await overrides.GetAsync(userId, Reach(organizationId), HttpContext.RequestAborted);
        var permission = model.Overrides.Count == 0
            ? AccessPermissions.DeleteUserOverrides
            : existing.Count == 0 ? AccessPermissions.CreateUserOverrides : AccessPermissions.UpdateUserOverrides;
        var resource = organizationId is null ? null : new OrganizationMemberResource(organizationId, userId);
        if (!await CanAsync(permission, organizationId, resource) || !await ManagesAsync(Reach(organizationId), userId))
        {
            return Forbid();
        }

        var granted = model.Overrides.Where(entry => entry.Level == OverrideLevel.Role && entry.Effect == OverrideEffect.Grant).Select(entry => entry.Name).ToArray();
        if (await RoleRefusalAsync(organizationId, granted) is { } refusal)
        {
            return BadRequest(new { error = refusal });
        }

        // Owner-locked permissions are never placed in an organization's overrides.
        if (organizationId is not null)
        {
            var locked = new HashSet<string>((await defaults.GetAsync(HttpContext.RequestAborted)).OwnerLockedPermissions, StringComparer.OrdinalIgnoreCase);
            var placed = model.Overrides.Where(entry => entry.Level == OverrideLevel.Permission && entry.Effect == OverrideEffect.Grant && locked.Contains(entry.Name)).Select(entry => entry.Name).ToArray();
            if (placed.Length > 0)
            {
                return BadRequest(new { error = $"Only the organization's owner holds {string.Join(", ", placed)}." });
            }
        }

        try
        {
            await overrides.SetAsync(userId, Reach(organizationId), model.Overrides, HttpContext.RequestAborted);
        }
        catch (InvalidOperationException refused) when (refused is AccessOverrideConflictException or OwnerProtectionException)
        {
            return BadRequest(new { error = refused.Message });
        }

        return Ok(new UserOverridesModel(await overrides.GetAsync(userId, Reach(organizationId), HttpContext.RequestAborted)));
    }

    /// <summary>What a user holds at a reach, with where each permission came from.</summary>
    public async Task<IResult> GetUserAccessAsync(string userId, [FromQuery] string? organizationId = null)
    {
        if (!await CanAsync(AccessPermissions.ReadUserOverrides, organizationId) || !await ManagesAsync(Reach(organizationId), userId))
        {
            return Forbid();
        }

        return await access.ReadAsync(userId, Reach(organizationId), HttpContext.RequestAborted) is { } resolved ? Ok(resolved) : NotFound();
    }

    // --- Organization defaults (tenant settings) ---

    public async Task<IResult> GetDefaultsAsync()
    {
        if (!await CanAsync(AccessPermissions.ReadGroups, null))
        {
            return Forbid();
        }

        return Ok(await defaults.GetAsync(HttpContext.RequestAborted));
    }

    public async Task<IResult> SetDefaultsAsync(OrganizationAccessDefaults model)
    {
        if (!await CanAsync(AccessPermissions.UpdateGroups, null))
        {
            return Forbid();
        }

        await defaults.SetAsync(model, HttpContext.RequestAborted);
        return Ok(await defaults.GetAsync(HttpContext.RequestAborted));
    }

    /// <summary>The roles an organization's admins may put in its groups: the tenant's
    /// assignable list, or every assignable role while the tenant has not set one.</summary>
    public async Task<IResult> AssignableRolesAsync([FromQuery] string organizationId)
    {
        if (!await CanAsync(AccessPermissions.ReadGroups, organizationId))
        {
            return Forbid();
        }

        return Ok(await AssignableAsync());
    }

    private async Task<IReadOnlyList<string>> AssignableAsync()
    {
        var configured = (await defaults.GetAsync(HttpContext.RequestAborted)).AssignableRoles;
        return configured.Count > 0
            ? configured
            : [.. (await roleService.GetAssignableRolesAsync()).Select(role => role.RoleName).OrderBy(name => name, StringComparer.OrdinalIgnoreCase)];
    }

    private async Task<string?> RoleRefusalAsync(string? organizationId, IEnumerable<string> roles)
    {
        if (organizationId is null)
        {
            return null;
        }

        var assignable = new HashSet<string>(await AssignableAsync(), StringComparer.OrdinalIgnoreCase);
        var outside = roles.Where(role => !assignable.Contains(role)).ToArray();
        return outside.Length == 0 ? null : $"An organization's groups may hold only the roles the tenant lets organizations assign; not {string.Join(", ", outside)}.";
    }

    /// <summary>The operation's CRUD permission: as it is at the tenant reach, against the
    /// organization (or the member, for the root guard) at an organization's reach.</summary>
    private Task<bool> CanAsync(Permission permission, string? organizationId, object? resource = null) =>
        decision.AllowsAsync(callers.Current, permission.Name, organizationId is null ? null : resource ?? new OrganizationResource(organizationId), HttpContext.RequestAborted);

    /// <summary>The hierarchy bound: the caller manages the user at the reach.</summary>
    private Task<bool> ManagesAsync(AccessReach reach, string userId) =>
        Hierarchy.ManagesAsync(callers.Current, reach, userId, HttpContext.RequestAborted);

    private static AccessReach Reach(string? organizationId) =>
        string.IsNullOrWhiteSpace(organizationId) ? AccessReach.Tenant : AccessReach.Organization(organizationId);

    private static void Apply(Role role, RoleWrite write)
    {
        role.RoleDescription = write.Description?.Trim() ?? role.RoleDescription;
        if (write.Sets is not null)
        {
            role.PermissionSets = write.Sets.Where(set => !string.IsNullOrWhiteSpace(set)).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        }

        if (write.Permissions is not null)
        {
            role.RoleClaims.RemoveAll(claim => claim.ClaimType == Permission.ClaimType);
            role.RoleClaims.AddRange(write.Permissions.Where(permission => !string.IsNullOrWhiteSpace(permission)).Distinct(StringComparer.OrdinalIgnoreCase).Select(RoleClaim.Create));
        }
    }

    private async Task<RoleDetail> ToDetailAsync(Role role) => new(
        role.RoleName,
        role.RoleDescription,
        role.PermissionSets,
        role.RoleClaims.Where(claim => claim.ClaimType == Permission.ClaimType).Select(claim => claim.ClaimValue).ToArray(),
        await roleService.IsAdminRoleAsync(role.RoleName),
        await roleService.IsSystemRoleAsync(role.RoleName));

    private static PermissionSetInfo ToInfo(PermissionSet set) => new(set.Name, set.Description, set.Permissions);
}
