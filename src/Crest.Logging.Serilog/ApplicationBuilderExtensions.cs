using Microsoft.AspNetCore.Builder;

namespace Crest.Logging;

public static class ApplicationBuilderExtensions
{
    public static IApplicationBuilder UseSerilogTenantNameLogging(this IApplicationBuilder builder)
    {
        return builder.UseMiddleware<SerilogTenantNameLoggingMiddleware>();
    }
}
