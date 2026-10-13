using Crest.Workflows.Extensions;
using Crest.Workflows.Http;
using Crest.Workflows.Security;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Crest.Environment.Shell.Configuration;
using Crest.Modules;

namespace Crest.Workflows.Features;

[Feature("Crest.Workflows.Http")]
public class HttpStartup(IShellConfiguration shellConfiguration) : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.ConfigureCrestWorkflows(crestWorkflows =>
        {
            crestWorkflows.UseHttp(http =>
            {
                http.ConfigureHttpOptions = options =>
                {
                    shellConfiguration.GetSection("CrestWorkflows:Http").Bind(options);
                };
                http.UseCache();
                // The engine's HTTP activities reach only public addresses, as the connectors do
                // (docs/access.md, recorded defects: SSRF); the shell's AllowPrivateNetworks opens it.
                http.HttpClientBuilder = builder => builder.ConfigurePrimaryHttpMessageHandler(sp =>
                    Connectors.ConnectorNetworkPolicy.CreateHandler(sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<Connectors.WorkflowConnectorOptions>>().Value.AllowPrivateNetworks));
                // An HTTP-endpoint workflow acts as the request's caller, and a trigger with
                // Authorize asks the one decision for Run on the definition (docs/operations.md
                // step 4). The engine's base path (/workflows by default) stays as it is.
                http.HttpEndpointAuthorizationHandler = sp => sp.GetRequiredService<HttpWorkflowEndpointAuthorizationHandler>();
            });
        });

        services
            .AddScoped<HttpWorkflowEndpointAuthorizationHandler>()
            .AddScoped<IHttpWorkflowInputContributor, HttpWorkflowActorContributor>();
    }

    public override void Configure(IApplicationBuilder app, IEndpointRouteBuilder routes, IServiceProvider serviceProvider)
    {
        app.UseWorkflows();
    }
}