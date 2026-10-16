using Grove.Access;
using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Grove.Workflows.Caching;
using Grove.Workflows.Management;
using Grove.Workflows.Management.Stores;
using Grove.Workflows.FieldSets;
using Grove.Workflows.Registry;
using Grove.Workflows.Services;
using Grove;
using Grove.Contents.Management;
using Grove.Security.Permissions;
using YesSql;

namespace Grove.Workflows.Security;

/// <summary>
/// The resource a workflow permission is evaluated against: the definition's own lists of
/// who may edit it, who may run it and who may read its instances (docs/workflows.md, phase
/// 5; the View list ruled 2026-10-13). Passed as the resource of
/// <c>AuthorizeAsync(user, permission, resource)</c>, so the check stays one Grove
/// authorization call; <see cref="WorkflowDefinitionAccessCeiling"/> is the part of the
/// decision that reads it.
/// </summary>
public sealed record WorkflowDefinitionAccessResource(string DefinitionId, IReadOnlyList<string> Edit, IReadOnlyList<string> Run, IReadOnlyList<string> View)
{
    public WorkflowDefinitionAccessResource(string definitionId, IReadOnlyList<string> edit, IReadOnlyList<string> run)
        : this(definitionId, edit, run, [])
    {
    }

    public static WorkflowDefinitionAccessResource From(string definitionId, IDictionary<string, object>? properties) =>
        new(definitionId, Names(properties, WorkflowsConstants.AccessEditProperty), Names(properties, WorkflowsConstants.AccessRunProperty), Names(properties, WorkflowsConstants.AccessViewProperty));

    /// <summary>A definition with no lists: tenant-wide, narrowed by nothing.</summary>
    public static WorkflowDefinitionAccessResource Open(string definitionId) => new(definitionId, [], [], []);

    public WorkflowDefinitionAccessModel ToModel() => new(Edit, Run, View);

    /// <summary>
    /// The veto the lists express, for a built caller: an edit permission against the Edit
    /// list, Run against the Run list, View (an instance read) against the View list, none
    /// implying another; a user name admits that user, a role name its members;
    /// an empty list admits everyone; the super user and administrators are never narrowed.
    /// The one access decision answers the permission only, so its direct callers consult
    /// this beside it (the ceiling makes the decision itself answer it).
    /// </summary>
    public bool Admits(Grove.Access.CallerContext caller, string permissionName)
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
        Set(properties, WorkflowsConstants.AccessViewProperty, access.View);
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
/// shipped, Publish) is vetoed by a definition's Edit list, Run by its Run list, View - an
/// instance read, asked with the definition as the resource - by its View list, when the
/// caller is named in neither (a user name is the per-user override; a role name admits its
/// members). A definition with no list changes nothing; the super user and the system are
/// never narrowed. Evaluated inside the decision, so the authorization-service path and the
/// direct callers of the decision get the same answer.
/// </summary>
public sealed class WorkflowDefinitionAccessCeiling : Grove.Access.IAccessCeiling
{
    private static readonly HashSet<string> EditPermissions = new(StringComparer.Ordinal)
    {
        WorkflowsConstants.Permissions.Edit,
        WorkflowsConstants.Permissions.ManageShipped,
        WorkflowsConstants.Permissions.Publish,
    };

    public string? Deny(Grove.Access.CallerContext caller, string permission, object? resource = null)
    {
        if (resource is not WorkflowDefinitionAccessResource definition || definition.Admits(caller, permission))
        {
            return null;
        }

        var what = EditPermissions.Contains(permission) ? "edit it" : permission == WorkflowsConstants.Permissions.View ? "read its instances" : "run it";
        return $"Workflow '{definition.DefinitionId}' limits who may {what}.";
    }

    /// <summary>The list a permission is checked against: Edit for the edit permissions, Run for Run, View for View, none for the rest.</summary>
    public static IReadOnlyList<string>? ListFor(string permissionName, WorkflowDefinitionAccessResource resource) =>
        EditPermissions.Contains(permissionName) ? resource.Edit
        : permissionName == WorkflowsConstants.Permissions.Run ? resource.Run
        : permissionName == WorkflowsConstants.Permissions.View ? resource.View
        : null;
}

/// <summary>Loads a definition's access lists (the API gate's view of the service below).</summary>
public interface IWorkflowDefinitionAccessReader
{
    Task<WorkflowDefinitionAccessResource?> GetAsync(string definitionId);

    /// <summary>Every definition's lists (the latest version of each), for the narrowing of an instance list.</summary>
    Task<IReadOnlyList<WorkflowDefinitionAccessResource>> ListAsync(CancellationToken cancellationToken = default);
}

/// <summary>Authorizes the gated caller against a definition in one decision (the ownership guard's view of the service below).</summary>
public interface IWorkflowDefinitionAuthorizer
{
    /// <summary>False when no gate set a caller: outside a request or a burst nothing is authorized.</summary>
    Task<bool> AuthorizeAsync(Permission permission, WorkflowDefinitionAccessResource resource);
}

/// <summary>
/// Reads and writes a definition's access lists, and authorizes the gated caller against a
/// definition in one decision (permission + the definition as resource).
/// </summary>
public sealed class WorkflowDefinitionAccessService(
    ICallerContextAccessor callers,
    IAccessDecision decision,
    IContentManager contentManager,
    ISession session,
    WorkflowDefinitionFieldSetMapper partMapper,
    IWorkflowDefinitionCacheManager definitionCacheManager,
    ICacheManager cacheManager,
    IWorkflowDefinitionStore definitions) : IWorkflowDefinitionAccessReader, IWorkflowDefinitionAuthorizer
{
    public async Task<IReadOnlyList<WorkflowDefinitionAccessResource>> ListAsync(CancellationToken cancellationToken = default) =>
        (await definitions.FindManyAsync(new Management.Filters.WorkflowDefinitionFilter { VersionOptions = Grove.Workflows.Common.Models.VersionOptions.Latest }, cancellationToken))
            .Select(definition => From(definition.DefinitionId, definition.CustomProperties))
            .ToList();

    private static WorkflowDefinitionAccessResource From(string definitionId, IDictionary<string, object>? properties) => WorkflowDefinitionAccessResource.From(definitionId, properties);

    public async Task<WorkflowDefinitionAccessResource?> GetAsync(string definitionId)
    {
        var contentItem = await contentManager.GetAsync(definitionId, VersionOptions.Latest);
        var part = contentItem?.Get<WorkflowDefinitionFieldSet>(ContentFieldSetName.Of<WorkflowDefinitionFieldSet>());
        return part is null || part.SerializedData is null ? null : WorkflowDefinitionAccessResource.From(definitionId, partMapper.MapModel(part).CustomProperties);
    }

    public async Task<bool> AuthorizeAsync(Permission permission, WorkflowDefinitionAccessResource resource) =>
        callers.Current is { } caller && (await decision.DecideAsync(caller, permission.Name, resource)).IsAllowed;

    public async Task<bool> AuthorizeAsync(Permission permission, string definitionId)
    {
        var resource = await GetAsync(definitionId) ?? WorkflowDefinitionAccessResource.Open(definitionId);
        return await AuthorizeAsync(permission, resource);
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

        var clean = new WorkflowDefinitionAccessModel(WorkflowDefinitionAccessResource.Clean(access.Edit), WorkflowDefinitionAccessResource.Clean(access.Run), WorkflowDefinitionAccessResource.Clean(access.View));
        foreach (var version in versions)
        {
            var model = partMapper.MapModel(version.Get<WorkflowDefinitionFieldSet>(ContentFieldSetName.Of<WorkflowDefinitionFieldSet>()));
            model.CustomProperties ??= new Dictionary<string, object>();
            WorkflowDefinitionAccessResource.Stamp(model.CustomProperties, clean);
            version.Alter<WorkflowDefinitionFieldSet>(part => partMapper.Map(model, part));
            await session.SaveAsync(version, cancellationToken: cancellationToken);
        }

        await definitionCacheManager.EvictWorkflowDefinitionAsync(definitionId, cancellationToken);
        await cacheManager.TriggerTokenAsync(typeof(CachingWorkflowDefinitionStore).FullName!, cancellationToken);
        return new(definitionId, clean.Edit, clean.Run, clean.View);
    }
}
