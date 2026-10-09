using Microsoft.AspNetCore.Http;
using Crest.Environment.Shell;
using Crest.Environment.Shell.Scope;
using Serilog.Context;

namespace Crest.Logging;

public class SerilogTenantNameLoggingMiddleware
{
    private readonly RequestDelegate _next;

    public SerilogTenantNameLoggingMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task Invoke(HttpContext context)
    {
        var tenantName = context.Features.Get<ShellContextFeature>()?.ShellContext.Settings.Name ?? ShellScope.Context?.Settings.Name ?? "None";

        using (LogContext.PushProperty("TenantName", tenantName))
        {
            await _next.Invoke(context);
        }
    }
}
