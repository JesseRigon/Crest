using System.Security.Claims;
using System.Text.Json;
using Crest.Workflows.Caching;
using Crest.Workflows.Management;
using Crest.Workflows.Management.Stores;
using Crest.Workflows.Parts;
using Crest.Workflows.Registry;
using Crest.Workflows.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Crest;
using Crest.ContentManagement;
using Crest.Security;
using Crest.Security.Permissions;
using YesSql;

namespace Crest.Workflows.Security;

/// <summary>
/// The resource a workflow permission is evaluated against: the definition's own lists of
/// who may edit and who may run it (docs/workflows.md, phase 5). Passed as the resource of
/// <c>AuthorizeAsync(user, permission, resource)</c>, so the check stays one Crest
/// authorization call; <see cref="WorkflowDefinitionAccessHandler"/> is the part of the
/// pipeline that reads it.
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
    /// this beside it (the authorization-service path has <see cref="WorkflowDefinitionAccessHandler"/>).
    /// </summary>
    public bool Admits(Crest.Access.CallerContext caller, string permissionName)
    {
        var list = WorkflowDefinitionAccessHandler.ListFor(permissionName, this);
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
/// Vetoes a workflow permission when the definition being authorized names who may hold it
/// and the caller is none of them: an edit permission (Edit, Manage shipped, Publish) against
/// the Edit list, Run against the Run list. A user name in a list is the per-user override;
/// a role name admits its members. A definition with no list changes nothing. The answer is
/// <see cref="WorkflowDefinitionAccessResource.Admits"/> on the request's one caller, the same
/// answer the direct callers of the decision get; a raw handler (runs despite prior
/// successes; Fail is sticky) so the role grant and every other handler still apply in the
/// same call. The super user is never narrowed by a list.
/// </summary>
public sealed class WorkflowDefinitionAccessHandler(Crest.Access.ICallerContextAccessor callerAccessor) : IAuthorizationHandler
{
    private static readonly HashSet<string> EditPermissions = new(StringComparer.Ordinal)
    {
        WorkflowsConstants.Permissions.Edit,
        WorkflowsConstants.Permissions.ManageShipped,
        WorkflowsConstants.Permissions.Publish,
    };

    public async Task HandleAsync(AuthorizationHandlerContext context)
    {
        if (context.Resource is not WorkflowDefinitionAccessResource resource || context.User?.Identity?.IsAuthenticated != true)
        {
            return;
        }

        var requirements = context.Requirements.OfType<PermissionRequirement>().ToList();
        if (requirements.Count == 0)
        {
            return;
        }

        var caller = callerAccessor.Current;
        if (caller is null)
        {
            // No gated caller: nothing to admit by; the lists narrow, they never grant.
            context.Fail(new AuthorizationFailureReason(this, $"Workflow '{resource.DefinitionId}' limits access and the request carries no caller."));
            return;
        }

        foreach (var requirement in requirements)
        {
            if (resource.Admits(caller, requirement.Permission.Name))
            {
                continue;
            }

            context.Fail(new AuthorizationFailureReason(this, $"Workflow '{resource.DefinitionId}' limits '{requirement.Permission.Name}' to named roles and users."));
        }

        await Task.CompletedTask;
    }

    /// <summary>The list a permission is narrowed by: Edit for the edit permissions, Run for Run, none otherwise.</summary>
    public static IReadOnlyList<string>? ListFor(string permissionName, WorkflowDefinitionAccessResource resource) =>
        EditPermissions.Contains(permissionName) ? resource.Edit
        : permissionName == WorkflowsConstants.Permissions.Run ? resource.Run
        : null;

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
