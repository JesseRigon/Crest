using Crest.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace Crest.Filters;

/// <summary>
/// A refused definition change (<see cref="CrestDefinitionLockedException"/>) is a 409 with
/// the reason, wherever it surfaces: Crest's own controllers and the stock content-types
/// admin alike (the decorators throw from inside the stock actions).
/// </summary>
public sealed class CrestDefinitionLockExceptionFilter : IExceptionFilter
{
    public void OnException(ExceptionContext context)
    {
        if (context.Exception is not CrestDefinitionLockedException locked)
        {
            return;
        }

        context.Result = new ObjectResult(new ProblemDetails
        {
            Title = "The definition is locked.",
            Detail = locked.Message,
            Status = StatusCodes.Status409Conflict,
        })
        { StatusCode = StatusCodes.Status409Conflict };
        context.ExceptionHandled = true;
    }
}
