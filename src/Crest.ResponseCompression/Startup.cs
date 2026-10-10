using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Crest.Modules;

namespace Crest.ResponseCompression;

public sealed class Startup : StartupBase
{
    public override int Order => -5;

    public override void Configure(IApplicationBuilder app, IEndpointRouteBuilder routes, IServiceProvider serviceProvider)
    {
        app.UseResponseCompression();
    }

    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddResponseCompression(options => options.EnableForHttps = true);
    }
}
