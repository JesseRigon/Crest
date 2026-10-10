using Crest.Workflows.Management.Models;
using Crest.Workflows.Management.Notifications;
using Crest.Workflows.Mediator.Contracts;
using Crest.Workflows.Units;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Crest.Workflows.Hooks;

/// <summary>Hook slots and their attachments. Reading takes View; attaching and detaching take Edit and the right to edit the attached flow.</summary>
[ApiController, AutoValidateAntiforgeryToken, Route(WorkflowsConstants.Routes.HooksApi)]
public sealed class HooksController(IAuthorizationService authorizationService, WorkflowHookService hooks) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> ListAsync(CancellationToken cancellationToken)
    {
        if (!await authorizationService.AuthorizeAsync(User, Permissions.ViewWorkflows))
        {
            return Forbid();
        }

        return Ok(await hooks.ListSlotsAsync(cancellationToken));
    }

    [HttpPost]
    public async Task<IActionResult> AttachAsync([FromBody] WorkflowHookAttachRequest request, CancellationToken cancellationToken)
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
            return Ok(await hooks.AttachAsync(User, request, cancellationToken));
        }
        catch (WorkflowHookAttachException ex)
        {
            return Problem(ex.Message, statusCode: StatusCodes.Status400BadRequest);
        }
    }

    [HttpDelete("{attachmentId}")]
    public async Task<IActionResult> DetachAsync(string attachmentId, CancellationToken cancellationToken)
    {
        if (!await authorizationService.AuthorizeAsync(User, Permissions.EditWorkflows))
        {
            return Forbid();
        }

        try
        {
            return await hooks.DetachAsync(User, attachmentId, cancellationToken) ? NoContent() : NotFound();
        }
        catch (WorkflowHookAttachException ex)
        {
            return Problem(ex.Message, statusCode: StatusCodes.Status403Forbidden);
        }
    }
}

/// <summary>
/// Publishing a definition that hook attachments use re-derives its atomicity: a version
/// that would stop being atomic (a wait or an external call added) is refused, with the
/// boundary named, because it would make the host's transaction impossible to keep. Hooked
/// into the engine's validation through its notification.
/// </summary>
public sealed class HookAttachmentAtomicityValidator(WorkflowHookService hooks, WorkflowAtomicityAnalyzer analyzer) : INotificationHandler<WorkflowDefinitionValidating>
{
    public async Task HandleAsync(WorkflowDefinitionValidating notification, CancellationToken cancellationToken)
    {
        var definitionId = notification.Workflow.Identity.DefinitionId;
        if (string.IsNullOrEmpty(definitionId) || (await hooks.AttachmentsOfAsync(definitionId)).Count == 0)
        {
            return;
        }

        // The version being published, not the stored one.
        var report = await analyzer.AnalyzeAsync(notification.Workflow, cancellationToken);
        if (report.IsAtomic)
        {
            return;
        }

        notification.ValidationErrors.Add(new WorkflowValidationError($"This flow is attached to a hook and must stay atomic (one transaction, no waits, no external calls): {report.Describe()}. Detach it from the hook or move those steps to a flow on the event side.", report.Boundaries[0].ActivityId));
    }
}
