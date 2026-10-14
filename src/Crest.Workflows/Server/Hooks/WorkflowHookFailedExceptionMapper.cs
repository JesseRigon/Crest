using Crest.Api;
using Microsoft.AspNetCore.Http;

namespace Crest.Workflows.Hooks;

/// <summary>
/// A hook attachment that refused a change answers 409 with the slot and the attachment
/// (docs/MVC/api.md › The error contract).
/// </summary>
public sealed class WorkflowHookFailedExceptionMapper : IApiExceptionMapper
{
    public IResult? Map(Exception exception, HttpContext httpContext) =>
        exception is WorkflowHookFailedException failed
            ? TypedResults.Problem(
                detail: failed.Message,
                statusCode: StatusCodes.Status409Conflict,
                title: "A hook attachment refused the change.",
                extensions: new Dictionary<string, object?> { ["slot"] = failed.Slot, ["attachment"] = failed.Attachment })
            : null;
}
