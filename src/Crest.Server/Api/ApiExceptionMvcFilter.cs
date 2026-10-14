using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace Crest.Api;

/// <summary>
/// The error contract for the APIs still hosted by MVC controllers (docs/MVC/api.md › Hosting):
/// the same <see cref="IApiExceptionMapper"/>s the route groups use answer an exception an action
/// throws. Goes with <c>AddControllers()</c>.
/// </summary>
internal sealed class ApiExceptionMvcFilter : IAsyncExceptionFilter
{
    public async Task OnExceptionAsync(ExceptionContext context)
    {
        if (context.ExceptionHandled || ApiExceptionEndpointFilter.Map(context.Exception, context.HttpContext) is not { } result)
        {
            return;
        }

        await result.ExecuteAsync(context.HttpContext);
        context.Result = new EmptyResult();
        context.ExceptionHandled = true;
    }
}
