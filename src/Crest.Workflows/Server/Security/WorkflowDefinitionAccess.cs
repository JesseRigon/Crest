using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Crest.Workflows.Caching;
using Crest.Workflows.Management;
using Crest.Workflows.Management.Stores;
using Crest.Workflows.Parts;
using Crest.Workflows.Registry;
using Crest.Workflows.Services;
using Microsoft.AspNetCore.Mvc;
using Crest;
using Crest.ContentManagement;
using Crest.Security.Permissions;
using YesSql;

namespace Crest.Workflows.Security;

/// <summary>
/// The resource a workflow permission is evaluated against: the definition's own lists of
/// who may edit and who may run it (docs/workflows.md, phase 5). Passed as the resource of
/// <c>AuthorizeAsync(user, permission, resource)</c>, so the check stays one Crest
/// authorization call; <see cref="WorkflowDefinitionAccessCeiling"/> is the part of the
/// decision that reads it.
/// </summary>
public sealed record WorkflowDefinitionAccessResource(string DefinitionId, IReadOnlyList<string> Edit, IReadOnlyList<string> Run)
{
    public static WorkflowDefinitionAccessResource From(string definitionId, IDictionary<string, object>? properties) =>
        new(definitionId, Names(properties, WorkflowsConstants.AccessEditProperty), Names(properties, WorkflowsConstants.AccessRunProperty));

    public WorkflowDefinitionAccessModel ToModel() => new(Edit, Run);

    /// <summary>
    /// The veto the lists express, for a built caller: an edit permission against the Edit
    /// list, Run against the Run list; a user name admits that user, a role name its members;
    /// an empty list admits everyone; the super user and administrators are never narrowed.
    /// The one access decision answers the permission only, so its direct callers consult
    /// this beside it (the ceiling makes the decision itself answer it).
    /// </summary>
    public bool Admits(Crest.Access.CallerContext caller, string permissionName)
    {
        var list = WorkflowDefinitionAccessCeiling.ListFor(permissionName, this);
        if (list is null || list.Count == 0 || caller.IsSuperUser || caller.IsSystem)
        {
            return true;
        }

        return list.Any(name => string.Equals(name, caller.UserName, StringComparison.OrdinalIgnoreCase) || caller.Roles.Contains(name));
    }

    /// <summary>Writes the lists onto custom properties; an empty list removes the property.</summary>
    public static void Stamp(IDictionary<string, object> properties, WorkflowDefinitionAccessModel access)
    {
        Set(properties, WorkflowsConstants.AccessEditProperty, access.Edit);
        Set(properties, WorkflowsConstants.AccessRunProperty, access.Run);
    }

    public static IReadOnlyList<string> Clean(IEnumerable<string>? names) =>
        (names ?? []).Select(n => n?.Trim() ?? string.Empty).Where(n => n.Length > 0).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();

    private static void Set(IDictionary<string, object> properties, string key, IEnumerable<string>? names)
    {
        var clean = Clean(names);
        if (clean.Count == 0)
        {
            properties.Remove(key);
        }
        else
        {
            properties[key] = clean;
        }
    }

    // Stored JSON yields a JsonElement array; in-memory writes yield a string collection.
    private static IReadOnlyList<string> Names(IDictionary<string, object>? properties, string key)
    {
        if (properties is null || !properties.TryGetValue(key, out var value) || value is null)
        {
            return [];
        }

        return value switch
        {
            JsonElement { ValueKind: JsonValueKind.Array } element => Clean(element.EnumerateArray().Select(e => e.ValueKind == JsonValueKind.String ? e.GetString() ?? string.Empty : e.GetRawText())),
            JsonElement { ValueKind: JsonValueKind.String } element => Clean((element.GetString() ?? string.Empty).Split(',')),
            string text => Clean(text.Split(',')),
            IEnumerable<string> names => Clean(names),
            IEnumerable<object> items => Clean(items.Select(i => i?.ToString() ?? string.Empty)),
            _ => [],
        };
    }
}

/// <summary>
/// The lists as the one decision's deny-only rule: an edit permission (Edit, Manage
/// shipped, Publish) is vetoed by a definition's Edit list, Run by its Run list, when the
/// caller is named in neither (a user name is the per-user override; a role name admits its
/// members). A definition with no list changes nothing; the super user and the system are
/// never narrowed. Evaluated inside the decision, so the authorization-service path and the
/// direct callers of the decision get the same answer.
/// </summary>
public sealed class WorkflowDefinitionAccessCeiling : Crest.Access.IAccessCeiling
{
    private static readonly HashSet<string> EditPermissions = new(StringComparer.Ordinal)
    {
        WorkflowsConstants.Permissions.Edit,
        WorkflowsConstants.Permissions.ManageShipped,
        WorkflowsConstants.Permissions.Publish,
    };

    public string? Deny(Crest.Access.CallerContext caller, string permission, object? resource = null)
    {
        if (resource is not WorkflowDefinitionAccessResource definition || definition.Admits(caller, permission))
        {
            return null;
        }

        return $"Workflow '{definition.DefinitionId}' limits who may {(EditPermissions.Contains(permission) ? "edit" : "run")} it.";
    }

    /// <summary>The list a permission is checked against: Edit for the edit permissions, Run for Run, none for the rest.</summary>
    public static IReadOnlyList<string>? ListFor(string permissionName, WorkflowDefinitionAccessResource resource)
    {
        if (EditPermissions.Contains(permissionName))
        {
            return resource.Edit;
        }

        return permissionName == WorkflowsConstants.Permissions.Run ? resource.Run : null;
    }
}

/// <summary>Loads a definition's access lists (the API gate's view of the service below).</summary>
public interface IWorkflowDefinitionAccessReader
{
    Task<WorkflowDefinitionAccessResource?> GetAsync(string definitionId);
}

/// <summary>Authorizes a user against a definition in one Crest call (the ownership guard's view of the service below).</summary>
public interface IWorkflowDefinitionAuthorizer
{
    Task<bool> AuthorizeAsync(ClaimsPrincipal user, Permission permission, WorkflowDefinitionAccessResource resource);
}

/// <summary>
/// Reads and writes a definition's access lists, and authorizes a user against a definition
/// in one Crest call (permission + the definition as resource).
/// </summary>
public sealed class WorkflowDefinitionAccessService(
    IAuthorizationService authorizationService,
    IContentManager contentManager,
    ISession session,
    WorkflowDefinitionPartMapper partMapper,
    IWorkflowDefinitionCacheManager definitionCacheManager,
    ICacheManager cacheManager) : IWorkflowDefinitionAccessReader, IWorkflowDefinitionAuthorizer
{
    public async Task<WorkflowDefinitionAccessResource?> GetAsync(string definitionId)
    {
        var contentItem = await contentManager.GetAsync(definitionId, VersionOptions.Latest);
        var part = contentItem?.Get<WorkflowDefinitionPart>(typeof(WorkflowDefinitionPart).Name);
        return part is null || part.SerializedData is null ? null : WorkflowDefinitionAccessResource.From(definitionId, partMapper.MapModel(part).CustomProperties);
    }

    public Task<bool> AuthorizeAsync(ClaimsPrincipal user, Permission permission, WorkflowDefinitionAccessResource resource) =>
        authorizationService.AuthorizeAsync(user, permission, resource);

    public async Task<bool> AuthorizeAsync(ClaimsPrincipal user, Permission permission, string definitionId)
    {
        var resource = await GetAsync(definitionId) ?? new WorkflowDefinitionAccessResource(definitionId, [], []);
        return await AuthorizeAsync(user, permission, resource);
    }

    /// <summary>
    /// Writes the lists onto every live version (latest and published) in place: no new
    /// draft, the flow's history is its logic, not its guards.
    /// </summary>
    public async Task<WorkflowDefinitionAccessResource?> SetAsync(string definitionId, WorkflowDefinitionAccessModel access, CancellationToken cancellationToken = default)
    {
        var versions = (await contentManager.GetAllVersionsAsync(definitionId)).Where(v => v.Latest || v.Published).ToList();
        if (versions.Count == 0)
        {
            return null;
        }

        var clean = new WorkflowDefinitionAccessModel(WorkflowDefinitionAccessResource.Clean(access.Edit), WorkflowDefinitionAccessResource.Clean(access.Run));
        foreach (var version in versions)
        {
            var model = partMapper.MapModel(version.Get<WorkflowDefinitionPart>(typeof(WorkflowDefinitionPart).Name));
            model.CustomProperties ??= new Dictionary<string, object>();
            WorkflowDefinitionAccessResource.Stamp(model.CustomProperties, clean);
            version.Alter<WorkflowDefinitionPart>(part => partMapper.Map(model, part));
            await session.SaveAsync(version, cancellationToken: cancellationToken);
        }

        await definitionCacheManager.EvictWorkflowDefinitionAsync(definitionId, cancellationToken);
        await cacheManager.TriggerTokenAsync(typeof(CachingWorkflowDefinitionStore).FullName!, cancellationToken);
        return new(definitionId, clean.Edit, clean.Run);
    }
}

/// <summary>Who may edit and run one definition. Reading takes View; writing takes the right to edit the definition itself.</summary>
[ApiController, AutoValidateAntiforgeryToken, Route(WorkflowsConstants.Routes.DefinitionsApi)]
public sealed class WorkflowDefinitionAccessController(WorkflowDefinitionAccessService access, WorkflowOwnershipGuard ownership, IAuthorizationService authorizationService) : ControllerBase
{
    [HttpGet(WorkflowsConstants.Routes.DefinitionAccessApi)]
    public async Task<IActionResult> GetAsync(string definitionId)
    {
        if (!await authorizationService.AuthorizeAsync(User, Permissions.ViewWorkflows))
        {
            return Forbid();
        }

        var resource = await access.GetAsync(definitionId);
        return resource is null ? NotFound() : Ok(resource.ToModel());
    }

    [HttpPut(WorkflowsConstants.Routes.DefinitionAccessApi)]
    public async Task<IActionResult> PutAsync(string definitionId, [FromBody] WorkflowDefinitionAccessModel model, CancellationToken cancellationToken)
    {
        var resource = await access.GetAsync(definitionId);
        if (resource is null)
        {
            return NotFound();
        }

        // The ownership tier first (a system flow's guards are code's too), then the user
        // against the definition as it stands: Manage shipped for a shipped flow, Edit for
        // the tenant's own, narrowed by the current Edit list.
        var tier = await ownership.GetStoredAsync(definitionId);
        if (tier.IsSystem)
        {
            return Forbid();
        }

        var permission = tier.IsShipped ? Permissions.ManageShippedWorkflows : Permissions.EditWorkflows;
        if (!await access.AuthorizeAsync(User, permission, resource))
        {
            return Forbid();
        }

        var saved = await access.SetAsync(definitionId, model, cancellationToken);
        return saved is null ? NotFound() : Ok(saved.ToModel());
    }
}
