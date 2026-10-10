using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace Crest.Workflows.Hooks;

/// <summary>
/// A required hook attachment that failed inside a service's request
/// (<see cref="WorkflowHookFailedException"/>, thrown through <see cref="IWorkflowHookRunner"/>)
/// is a 409 with the slot, the attachment and the reason: the object was not created, since
/// the unit was failed and its session cancelled. Any module's controller gets this answer.
/// </summary>
public sealed class WorkflowHookFailedExceptionFilter : IExceptionFilter
{
    public void OnException(ExceptionContext context)
    {
        if (context.Exception is not WorkflowHookFailedException failed)
        {
            return;
        }

        context.Result = new ObjectResult(new ProblemDetails
        {
            Title = "A hook attachment refused the change.",
            Detail = failed.Message,
            Status = StatusCodes.Status409Conflict,
            Extensions = { ["slot"] = failed.Slot, ["attachment"] = failed.Attachment },
        })
        { StatusCode = StatusCodes.Status409Conflict };
        context.ExceptionHandled = true;
    }
}
