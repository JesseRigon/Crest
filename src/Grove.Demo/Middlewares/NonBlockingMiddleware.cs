using Microsoft.AspNetCore.Http;

namespace Grove.Demo;

public class NonBlockingMiddleware
{
    private readonly RequestDelegate _next;

    public NonBlockingMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task Invoke(HttpContext httpContext)
    {
        // If the tenant pipeline is re-executed on error, the header is already set.
        httpContext.Response.Headers["Grove"] = "2.0";
        await _next.Invoke(httpContext);
    }
}
