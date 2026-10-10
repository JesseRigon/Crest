using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Crest.Modules;

namespace Crest.Mvc.HelloWorld;

public sealed class Startup : StartupBase
{
    private readonly IConfiguration _configuration;

    public Startup(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    public override void Configure(IApplicationBuilder builder, IEndpointRouteBuilder routes, IServiceProvider serviceProvider)
    {
        if (string.IsNullOrEmpty(_configuration["Sample"]))
        {
            throw new Exception(":(");
        }

        routes.MapAreaControllerRoute
        (
            name: "Home",
            areaName: "Crest.Mvc.HelloWorld",
            pattern: "",
            defaults: new { controller = "Home", action = "Index" }
        );
    }
}
