using Grove.Infrastructure.Shell;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;

namespace Grove.Display;

/// <summary>
/// The server side of <see cref="IShellResponseStatus"/>: the prerendering request's status. It is
/// applied as the response starts, after the page has rendered: Blazor writes no body for a page
/// that set 404 while rendering (it leaves the response to the status code pages), and this page
/// renders its own error page (the layout with NotFound). The status code pages are switched off
/// for the response for the same reason.
/// </summary>
public sealed class ShellResponseStatus(IHttpContextAccessor httpContextAccessor) : IShellResponseStatus
{
    public void Set(int statusCode)
    {
        if (httpContextAccessor.HttpContext is not { Response.HasStarted: false } context)
        {
            return;
        }

        if (context.Features.Get<IStatusCodePagesFeature>() is { } statusCodePages)
        {
            statusCodePages.Enabled = false;
        }

        context.Response.OnStarting(() =>
        {
            context.Response.StatusCode = statusCode;
            return Task.CompletedTask;
        });
    }
}
