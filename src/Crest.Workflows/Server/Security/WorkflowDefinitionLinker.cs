using Crest.Workflows.Api;
using Crest.Workflows.Api.Models;
using Crest.Workflows.Api.Services;
using Crest.Workflows.Management.Entities;
using Crest.Workflows.Management.Models;
using Crest.Workflows.Models;
using Crest.Workflows.Registry;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace Crest.Workflows.Security;

/// <summary>
/// The engine's definition links (what Studio reads to decide read-only: no <c>publish</c>
/// link, no editing) narrowed to what the acting user may actually do to that definition
/// (docs/workflows.md, phase 5): a system flow offers no write links to anyone, a shipped
/// one only to Manage shipped holders, a tenant one only to Edit/Publish holders, each
/// narrowed by the definition's own access lists - the same authorization call the store
/// makes when the write arrives, so the designer never offers what the gate refuses.
/// </summary>
public sealed class WorkflowDefinitionLinker(IServiceProvider serviceProvider, IHttpContextAccessor httpContextAccessor, IAuthorizationService authorizationService) : IWorkflowDefinitionLinker
{
    private static readonly HashSet<string> EditRels = new(StringComparer.OrdinalIgnoreCase) { "delete", "import", "update-references" };
    private static readonly HashSet<string> PublishRels = new(StringComparer.OrdinalIgnoreCase) { "publish", "retract" };

    private readonly StaticWorkflowDefinitionLinker _inner = ActivatorUtilities.CreateInstance<StaticWorkflowDefinitionLinker>(serviceProvider);

    public async Task<LinkedWorkflowDefinitionModel> MapAsync(WorkflowDefinition definition, CancellationToken cancellationToken = default)
    {
        var linked = await _inner.MapAsync(definition, cancellationToken);
        return new LinkedWorkflowDefinitionModel(await NarrowAsync(linked.Links, definition.DefinitionId, definition.CustomProperties))
        {
            Id = linked.Id,
            DefinitionId = linked.DefinitionId,
            Name = linked.Name,
            Description = linked.Description,
            CreatedAt = linked.CreatedAt,
            Version = linked.Version,
            ToolVersion = linked.ToolVersion,
            Variables = linked.Variables,
            Inputs = linked.Inputs,
            Outputs = linked.Outputs,
            Outcomes = linked.Outcomes,
            CustomProperties = linked.CustomProperties,
            IsReadonly = linked.IsReadonly,
            IsSystem = linked.IsSystem,
            IsLatest = linked.IsLatest,
            IsPublished = linked.IsPublished,
            Options = linked.Options,
            UsableAsActivity = linked.UsableAsActivity,
            Root = linked.Root,
        };
    }

    // Summaries carry no custom properties; the list keeps the engine's links (Studio decides
    // read-only per definition, on the full model above).
    public PagedListResponse<LinkedWorkflowDefinitionSummary> MapAsync(PagedListResponse<WorkflowDefinitionSummary> list, CancellationToken cancellationToken = default) =>
        _inner.MapAsync(list, cancellationToken);

    public async Task<List<LinkedWorkflowDefinitionModel>> MapAsync(List<WorkflowDefinition> definitions, CancellationToken cancellationToken = default)
    {
        var result = new List<LinkedWorkflowDefinitionModel>(definitions.Count);
        foreach (var definition in definitions)
        {
            result.Add(await MapAsync(definition, cancellationToken));
        }

        return result;
    }

    private async Task<Link[]?> NarrowAsync(Link[]? links, string definitionId, IDictionary<string, object>? properties)
    {
        if (links is null || links.Length == 0)
        {
            return links;
        }

        var user = httpContextAccessor.HttpContext?.User;
        if (user?.Identity?.IsAuthenticated != true)
        {
            return links;
        }

        var ownership = WorkflowOwnershipInfo.From(properties);
        if (ownership.IsSystem)
        {
            return links.Where(l => !EditRels.Contains(l.Rel) && !PublishRels.Contains(l.Rel)).ToArray();
        }

        var resource = WorkflowDefinitionAccessResource.From(definitionId, properties);
        var canEdit = await authorizationService.AuthorizeAsync(user, WorkflowOwnershipGuard.PermissionFor(WorkflowChange.Save, ownership), resource);
        var canPublish = await authorizationService.AuthorizeAsync(user, WorkflowOwnershipGuard.PermissionFor(WorkflowChange.Publish, ownership), resource);
        return links.Where(l => (canEdit || !EditRels.Contains(l.Rel)) && (canPublish || !PublishRels.Contains(l.Rel))).ToArray();
    }
}
