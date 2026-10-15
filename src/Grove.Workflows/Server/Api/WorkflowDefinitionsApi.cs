using Grove.Api;
using Grove.Workflows.Fields;
using Grove.Workflows.Registry;
using Grove.Workflows.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace Grove.Workflows.Api;

/// <summary>
/// <c>api/grove/workflows/definitions</c>: one definition's access lists (who may edit and run
/// it, and read its instances: reading takes View, writing the right to edit the definition
/// itself) and its field dependencies resolved against the tenant's definitions now (the "what
/// this flow needs" panel; reading takes View).
/// </summary>
public sealed class WorkflowDefinitionsApi : ApiContributor
{
    public override string Name => "WorkflowDefinitions";

    public override string FeatureId => WorkflowsConstants.FeatureId;

    public override void Map(IEndpointRouteBuilder group)
    {
        var routes = group.MapGroup("workflows/definitions");
        routes.MapGet("{definitionId}/access", (HttpContext http, [FromRoute] string definitionId) =>
            ApiHandlers.For<WorkflowDefinitionsHandlers>(http).GetAccessAsync(definitionId));
        routes.MapPut("{definitionId}/access", (HttpContext http, [FromRoute] string definitionId, [FromBody] WorkflowDefinitionAccessModel model, CancellationToken cancellationToken) =>
            ApiHandlers.For<WorkflowDefinitionsHandlers>(http).PutAccessAsync(definitionId, model, cancellationToken));
        routes.MapGet("{definitionId}/field-dependencies", (HttpContext http, [FromRoute] string definitionId, CancellationToken cancellationToken) =>
            ApiHandlers.For<WorkflowDefinitionsHandlers>(http).GetFieldDependenciesAsync(definitionId, cancellationToken));
    }
}

public sealed class WorkflowDefinitionsHandlers(
    WorkflowDefinitionAccessService access,
    WorkflowOwnershipGuard ownership,
    WorkflowFieldDependencyAnalyzer analyzer,
    IAuthorizationService authorizationService) : ApiHandlers
{
    public async Task<IResult> GetAccessAsync(string definitionId)
    {
        if (!await authorizationService.AuthorizeAsync(User, Permissions.ViewWorkflows))
        {
            return Forbid();
        }

        var resource = await access.GetAsync(definitionId);
        return resource is null ? NotFound() : Ok(resource.ToModel());
    }

    public async Task<IResult> PutAccessAsync(string definitionId, WorkflowDefinitionAccessModel model, CancellationToken cancellationToken)
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
        if (!await access.AuthorizeAsync(permission, resource))
        {
            return Forbid();
        }

        var saved = await access.SetAsync(definitionId, model, cancellationToken);
        return saved is null ? NotFound() : Ok(saved.ToModel());
    }

    public async Task<IResult> GetFieldDependenciesAsync(string definitionId, CancellationToken cancellationToken)
    {
        if (!await authorizationService.AuthorizeAsync(User, Permissions.ViewWorkflows))
        {
            return Forbid();
        }

        var report = await analyzer.AnalyzeAsync(definitionId, cancellationToken);
        return report is null ? NotFound() : Ok(report);
    }
}
