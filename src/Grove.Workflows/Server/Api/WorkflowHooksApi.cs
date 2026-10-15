using Grove.Api;
using Grove.Workflows.Hooks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace Grove.Workflows.Api;

/// <summary>
/// <c>api/grove/workflows/hooks</c>: hook slots and their attachments. Reading takes View;
/// attaching and detaching take Edit and the right to edit the attached flow.
/// </summary>
public sealed class WorkflowHooksApi : ApiContributor
{
    public override string Name => "WorkflowHooks";

    public override string FeatureId => WorkflowsConstants.FeatureId;

    public override void Map(IEndpointRouteBuilder group)
    {
        var routes = group.MapGroup("workflows/hooks");
        routes.MapGet("", (HttpContext http, CancellationToken cancellationToken) => ApiHandlers.For<WorkflowHooksHandlers>(http).ListAsync(cancellationToken));
        routes.MapGet("candidates", (HttpContext http, CancellationToken cancellationToken) => ApiHandlers.For<WorkflowHooksHandlers>(http).CandidatesAsync(cancellationToken));
        routes.MapPost("", (HttpContext http, [FromBody] WorkflowHookAttachRequest request, CancellationToken cancellationToken) =>
            ApiHandlers.For<WorkflowHooksHandlers>(http).AttachAsync(request, cancellationToken));
        routes.MapDelete("{attachmentId}", (HttpContext http, [FromRoute] string attachmentId, CancellationToken cancellationToken) =>
            ApiHandlers.For<WorkflowHooksHandlers>(http).DetachAsync(attachmentId, cancellationToken));
    }
}

public sealed class WorkflowHooksHandlers(IAuthorizationService authorizationService, WorkflowHookService hooks) : ApiHandlers
{
    public async Task<IResult> ListAsync(CancellationToken cancellationToken)
    {
        if (!await authorizationService.AuthorizeAsync(User, Permissions.ViewWorkflows))
        {
            return Forbid();
        }

        return Ok(await hooks.ListSlotsAsync(cancellationToken));
    }

    /// <summary>The published flows the user may attach (the hook attachment page's picker).</summary>
    public async Task<IResult> CandidatesAsync(CancellationToken cancellationToken)
    {
        if (!await authorizationService.AuthorizeAsync(User, Permissions.EditWorkflows))
        {
            return Forbid();
        }

        return Ok(await hooks.ListCandidatesAsync(cancellationToken));
    }

    public async Task<IResult> AttachAsync(WorkflowHookAttachRequest request, CancellationToken cancellationToken)
    {
        if (!await authorizationService.AuthorizeAsync(User, Permissions.EditWorkflows))
        {
            return Forbid();
        }

        if (string.IsNullOrWhiteSpace(request.Slot) || string.IsNullOrWhiteSpace(request.DefinitionId))
        {
            return Problem("A slot and a definition id are required.", statusCode: StatusCodes.Status400BadRequest);
        }

        try
        {
            return Ok(await hooks.AttachAsync(request, cancellationToken));
        }
        catch (WorkflowHookAttachException ex)
        {
            return Problem(ex.Message, statusCode: StatusCodes.Status400BadRequest);
        }
    }

    public async Task<IResult> DetachAsync(string attachmentId, CancellationToken cancellationToken)
    {
        if (!await authorizationService.AuthorizeAsync(User, Permissions.EditWorkflows))
        {
            return Forbid();
        }

        try
        {
            return await hooks.DetachAsync(attachmentId, cancellationToken) ? NoContent() : NotFound();
        }
        catch (WorkflowHookAttachException ex)
        {
            return Problem(ex.Message, statusCode: StatusCodes.Status403Forbidden);
        }
    }
}
