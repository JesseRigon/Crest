using Microsoft.Extensions.Hosting;
using Serilog;

namespace Crest.Logging;

public static class WebHostBuilderExtensions
{
    public static IHostBuilder UseSerilogWeb(this IHostBuilder builder)
    {
        return builder.UseSerilog((hostingContext, configBuilder) =>
        {
            configBuilder.ReadFrom.Configuration(hostingContext.Configuration)
            .Enrich.FromLogContext();
        });
    }
}
