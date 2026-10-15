using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Grove.HealthChecks.Services;

public interface IHealthChecksResponseWriter
{
    Task WriteResponseAsync(HttpContext context, HealthReport report);
}
