using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Crest.HealthChecks.Services;

public interface IHealthChecksResponseWriter
{
    Task WriteResponseAsync(HttpContext context, HealthReport report);
}
