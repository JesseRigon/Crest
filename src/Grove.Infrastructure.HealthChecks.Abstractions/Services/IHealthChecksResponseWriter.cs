using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Grove.Infrastructure.HealthChecks.Services;

public interface IHealthChecksResponseWriter
{
    Task WriteResponseAsync(HttpContext context, HealthReport report);
}
