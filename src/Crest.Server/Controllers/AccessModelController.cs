using Crest.Access;
using Crest.Roles;
using Crest.Security;
using Crest.Security.Permissions;
using Crest.Security.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace Crest.Controllers;

public sealed record PermissionInfo(string Name, string? Description, string? Category, bool IsSecurityCritical);

public sealed record PermissionSetInfo(string Name, string? Description, IReadOnlyList<string> Permissions);

public sealed record RoleDetail(string Name, string? Description, IReadOnlyList<string> Sets, IReadOnlyList<string> Permissions, bool IsAdmin, bool IsSystem);

public sealed record RoleWrite(string Name, string? Description, IReadOnlyList<string>? Sets, IReadOnlyList<string>? Permissions);

public sealed record GroupUserRequest(string UserId);

public sealed record UserOverridesModel(IReadOnlyList<UserAccessOverride> Overrides);

/// <summary>
/// The editors' API for the permission model (docs/permissions.md › Editors): the permission
/// catalog, permission sets, roles with their sets and loose permissions, groups at a reach,
/// per-user overrides and the organization defaults. Tenant-reach writes take
/// <c>ManageRoles</c>; an organization's groups and overrides take <c>ManageOrgMembers</c>
/// against the organization, so staff edit every organization's and an organization's admin
/// only their own. An organization's groups hold only roles from the tenant's assignable list.
/// </summary>
[ApiController]
[AutoValidateAntiforgeryToken]
[Route("api/crest")]
public sealed class AccessModelController(
    IAccessDecision decision,
    ICallerContextAccessor callers,
    IPermissionService permissions,
    IPermissionSetService sets,
    RoleManager<IRole> roleManager,
    Crest.Security.Services.IRoleService roleService,
    IAccessGroupService groups,
    IUserAccessOverrideService overrides,
    IOrganizationAccessDefaultsService defaults,
    IUserAccessReader access) : ControllerBase
{
    // --- Catalog and sets (tenant schema) ---

    [HttpGet("permission-catalog")]
    public async Task<ActionResult<IReadOnlyList<PermissionInfo>>> CatalogAsync([FromQuery] string? organizationId = null)
    {
        if (!await CanEditAsync(organizationId))
        {
            return Forbid();
        }

        return Ok((await permissions.GetPermissionsAsync())
            .Select(permission => new PermissionInfo(permission.Name, permission.Description, permission.Category, permission.IsSecurityCritical))
            .DistinctBy(permission => permission.Name)
            .OrderBy(permission => permission.Category).ThenBy(permission => permission.Name)
            .ToArray());
    }

    [HttpGet("permission-sets")]
    public async Task<ActionResult<IReadOnlyList<PermissionSetInfo>>> ListSetsAsync()
    {
        if (!await CanEditAsync(null))
        {
            return Forbid();
        }

        return Ok((await sets.ListAsync(HttpContext.RequestAborted)).Select(ToInfo).ToArray());
    }

    [HttpPut("permission-sets/{name}")]
    public async Task<ActionResult<PermissionSetInfo>> SaveSetAsync(string name, PermissionSetInfo write)
    {
        if (!await CanEditAsync(null))
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

    [HttpDelete("permission-sets/{name}")]
    public async Task<IActionResult> DeleteSetAsync(string name)
    {
        if (!await CanEditAsync(null))
        {
            return Forbid();
        }

        return await sets.DeleteAsync(name, HttpContext.RequestAborted) ? NoContent() : NotFound();
    }

    // --- Roles: sets and loose permissions ---

    [HttpGet("roles/{name}")]
    public async Task<ActionResult<RoleDetail>> GetRoleAsync(string name)
    {
        if (!await CanEditAsync(null))
        {
            return Forbid();
        }

        return await roleManager.FindByNameAsync(name) is Role role ? Ok(await ToDetailAsync(role)) : NotFound();
    }

    [HttpPost("roles")]
    public async Task<ActionResult<RoleDetail>> CreateRoleAsync(RoleWrite write)
    {
        if (!await CanEditAsync(null))
        {
            return Forbid();
        }

        if (string.IsNullOrWhiteSpace(write.Name) || await roleManager.FindByNameAsync(write.Name) is not null)
        {
            return BadRequest(new { error = "The role needs a name no other role has." });
        }

        var role = new Role { RoleName = write.Name.Trim() };
        Apply(role, write);
        var result = await roleManager.CreateAsync(role);
        return result.Succeeded ? Ok(await ToDetailAsync(role)) : BadRequest(new { error = string.Join(" ", result.Errors.Select(error => error.Description)) });
    }

    [HttpPut("roles/{name}")]
    public async Task<ActionResult<RoleDetail>> UpdateRoleAsync(string name, RoleWrite write)
    {
        if (!await CanEditAsync(null))
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

    [HttpDelete("roles/{name}")]
    public async Task<IActionResult> DeleteRoleAsync(string name)
    {
        if (!await CanEditAsync(null))
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

    [HttpGet("access/groups")]
    public async Task<ActionResult<IReadOnlyList<AccessGroupModel>>> ListGroupsAsync([FromQuery] string? organizationId = null)
    {
        if (!await CanEditAsync(organizationId))
        {
            return Forbid();
        }

        return Ok(await groups.ListAsync(Reach(organizationId), HttpContext.RequestAborted));
    }

    [HttpPost("access/groups")]
    public async Task<ActionResult<AccessGroupModel>> CreateGroupAsync([FromQuery] string? organizationId, AccessGroupWrite write)
    {
        if (!await CanEditAsync(organizationId))
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

    [HttpPut("access/groups/{groupId}")]
    public async Task<ActionResult<AccessGroupModel>> UpdateGroupAsync(string groupId, AccessGroupWrite write)
    {
        if (await groups.GetAsync(groupId, HttpContext.RequestAborted) is not { } group)
        {
            return NotFound();
        }

        if (!await CanEditAsync(group.Reach.OrganizationId))
        {
            return Forbid();
        }

        if (await RoleRefusalAsync(group.Reach.OrganizationId, write.Roles) is { } refusal)
        {
            return BadRequest(new { error = refusal });
        }

        return Ok(await groups.UpdateAsync(groupId, write, HttpContext.RequestAborted));
    }

    [HttpDelete("access/groups/{groupId}")]
    public async Task<IActionResult> DeleteGroupAsync(string groupId)
    {
        if (await groups.GetAsync(groupId, HttpContext.RequestAborted) is not { } group)
        {
            return NotFound();
        }

        if (!await CanEditAsync(group.Reach.OrganizationId))
        {
            return Forbid();
        }

        await groups.DeleteAsync(groupId, HttpContext.RequestAborted);
        return NoContent();
    }

    [HttpPost("access/groups/{groupId}/users")]
    public async Task<IActionResult> AddGroupUserAsync(string groupId, GroupUserRequest request)
    {
        if (await groups.GetAsync(groupId, HttpContext.RequestAborted) is not { } group)
        {
            return NotFound();
        }

        // An organization's group takes that organization's members only (the root guard):
        // staff are never members of an organization's groups.
        var resource = group.Reach.IsTenant ? null : (object)new OrganizationMemberResource(group.Reach.OrganizationId!, request.UserId);
        if (!await CanEditAsync(group.Reach.OrganizationId, resource))
        {
            return Forbid();
        }

        await groups.AddUserAsync(groupId, request.UserId, HttpContext.RequestAborted);
        return NoContent();
    }

    [HttpDelete("access/groups/{groupId}/users/{userId}")]
    public async Task<IActionResult> RemoveGroupUserAsync(string groupId, string userId)
    {
        if (await groups.GetAsync(groupId, HttpContext.RequestAborted) is not { } group)
        {
            return NotFound();
        }

        if (!await CanEditAsync(group.Reach.OrganizationId))
        {
            return Forbid();
        }

        await groups.RemoveUserAsync(groupId, userId, HttpContext.RequestAborted);
        return NoContent();
    }

    // --- Per-user overrides and the resolved view ---

    [HttpGet("access/users/{userId}/overrides")]
    public async Task<ActionResult<UserOverridesModel>> GetOverridesAsync(string userId, [FromQuery] string? organizationId = null)
    {
        if (!await CanEditAsync(organizationId))
        {
            return Forbid();
        }

        return Ok(new UserOverridesModel(await overrides.GetAsync(userId, Reach(organizationId), HttpContext.RequestAborted)));
    }

    [HttpPut("access/users/{userId}/overrides")]
    public async Task<ActionResult<UserOverridesModel>> SetOverridesAsync(string userId, [FromQuery] string? organizationId, UserOverridesModel model)
    {
        var resource = organizationId is null ? null : (object)new OrganizationMemberResource(organizationId, userId);
        if (!await CanEditAsync(organizationId, resource))
        {
            return Forbid();
        }

        var granted = model.Overrides.Where(entry => entry.Level == OverrideLevel.Role && entry.Effect == OverrideEffect.Grant).Select(entry => entry.Name).ToArray();
        if (await RoleRefusalAsync(organizationId, granted) is { } refusal)
        {
            return BadRequest(new { error = refusal });
        }

        try
        {
            await overrides.SetAsync(userId, Reach(organizationId), model.Overrides, HttpContext.RequestAborted);
        }
        catch (AccessOverrideConflictException conflict)
        {
            return BadRequest(new { error = conflict.Message });
        }

        return Ok(new UserOverridesModel(await overrides.GetAsync(userId, Reach(organizationId), HttpContext.RequestAborted)));
    }

    /// <summary>What a user holds at a reach, with where each permission came from.</summary>
    [HttpGet("access/users/{userId}")]
    public async Task<ActionResult<UserAccess>> GetUserAccessAsync(string userId, [FromQuery] string? organizationId = null)
    {
        if (!await CanEditAsync(organizationId))
        {
            return Forbid();
        }

        return await access.ReadAsync(userId, Reach(organizationId), HttpContext.RequestAborted) is { } resolved ? Ok(resolved) : NotFound();
    }

    // --- Organization defaults (tenant settings) ---

    [HttpGet("access/organization-defaults")]
    public async Task<ActionResult<OrganizationAccessDefaults>> GetDefaultsAsync()
    {
        if (!await CanEditAsync(null))
        {
            return Forbid();
        }

        return Ok(await defaults.GetAsync(HttpContext.RequestAborted));
    }

    [HttpPut("access/organization-defaults")]
    public async Task<ActionResult<OrganizationAccessDefaults>> SetDefaultsAsync(OrganizationAccessDefaults model)
    {
        if (!await CanEditAsync(null))
        {
            return Forbid();
        }

        await defaults.SetAsync(model, HttpContext.RequestAborted);
        return Ok(await defaults.GetAsync(HttpContext.RequestAborted));
    }

    /// <summary>The roles an organization's admins may put in its groups: the tenant's
    /// assignable list, or every assignable role while the tenant has not set one.</summary>
    [HttpGet("access/assignable-roles")]
    public async Task<ActionResult<IReadOnlyList<string>>> AssignableRolesAsync([FromQuery] string organizationId)
    {
        if (!await CanEditAsync(organizationId))
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

    /// <summary>The tenant reach takes ManageRoles; an organization's takes ManageOrgMembers against it.</summary>
    private async Task<bool> CanEditAsync(string? organizationId, object? resource = null)
    {
        var caller = callers.Current;
        if (organizationId is null)
        {
            return await decision.AllowsAsync(caller, RolesPermissions.ManageRoles.Name, null, HttpContext.RequestAborted);
        }

        return await decision.AllowsAsync(caller, OrganizationPermissionNames.ManageOrgMembers, resource ?? new OrganizationResource(organizationId), HttpContext.RequestAborted);
    }

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
