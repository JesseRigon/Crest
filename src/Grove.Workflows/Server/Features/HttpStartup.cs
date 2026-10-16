using Grove.Workflows.Extensions;
using Grove.Workflows.Http;
using Grove.Workflows.Security;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Grove.Environment.Shell.Configuration;
using Grove.Modules;

namespace Grove.Workflows.Features;

[Feature("Grove.Workflows.Http")]
public class HttpStartup(IShellConfiguration shellConfiguration) : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.ConfigureGroveWorkflows(groveWorkflows =>
        {
            groveWorkflows.UseHttp(http =>
            {
                http.ConfigureHttpOptions = options =>
                {
                    shellConfiguration.GetSection("GroveWorkflows:Http").Bind(options);
                };
                http.UseCache();
                // The engine's HTTP activities reach only public addresses, as connections do
                // (docs/access.md, recorded defects: SSRF): the connector engine's network guard,
                // opened by the shell's Connectors:AllowPrivateNetworks. Redirects are followed
                // here (each hop is guarded); connections never follow them.
                http.HttpClientBuilder = builder => builder.ConfigurePrimaryHttpMessageHandler(sp =>
                {
                    var handler = Grove.Data.Protocols.Http.NetworkGuard.CreateHandler(sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<Grove.Data.Connectors.ConnectorOptions>>().Value.AllowPrivateNetworks);
                    handler.AllowAutoRedirect = true;
                    return handler;
                });
                // An HTTP-endpoint workflow acts as the request's caller, and a trigger with
                // Authorize asks the one decision for Run on the definition (docs/operations.md
                // step 4). The engine's base path (/workflows by default) stays as it is.
                http.HttpEndpointAuthorizationHandler = sp => sp.GetRequiredService<HttpWorkflowEndpointAuthorizationHandler>();
            });
        });

        services
            .AddScoped<HttpWorkflowEndpointAuthorizationHandler>()
            .AddScoped<IHttpWorkflowInputContributor, HttpWorkflowActorContributor>();

        // The workflows' HTTP endpoints are defined entries of the tenant's API registry
        // (docs/MVC/api.md P15), reloaded when the engine indexes a change to them.
        services.AddDefinedApiSource<Api.WorkflowHttpEndpointSource>();
        services.AddScoped<IHttpEndpointRoutesObserver, Api.WorkflowHttpEndpointsInvalidator>();
    }
}