using Crest.Api;
using Crest.Workflows.Http;
using Crest.Workflows.Http.Options;
using Crest.Workflows.Http.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Crest.Workflows.Api;

/// <summary>
/// The workflows' HTTP endpoints as the API registry's defined entries (docs/MVC/api.md P15): every
/// route an <c>HttpEndpoint</c> trigger or waiting bookmark of this tenant answers, served by the
/// engine's <see cref="HttpWorkflowRequestHandler"/>, plus a catch-all under the engine's base path
/// that answers 404 for a workflow route nothing answers. Reloaded when the engine indexes a change
/// to those triggers or bookmarks (<see cref="WorkflowHttpEndpointsInvalidator"/>).
/// </summary>
public sealed class WorkflowHttpEndpointSource(IHttpEndpointRoutes routes, IOptions<HttpActivityOptions> options) : IDefinedApiSource
{
    public const string SourceName = "workflows.http";

    public string Name => SourceName;

    public async Task<IReadOnlyList<DefinedApiEntry>> GetEntriesAsync(CancellationToken cancellationToken = default)
    {
        var entries = new List<DefinedApiEntry>();
        foreach (var route in await routes.ListAsync(cancellationToken))
        {
            var template = route.Route;
            entries.Add(new DefinedApiEntry(
                Id: template,
                Source: SourceName,
                RoutePattern: "/" + template.Trim('/'),
                HttpMethods: [],
                Handler: context => context.RequestServices.GetRequiredService<HttpWorkflowRequestHandler>().HandleAsync(context, template),
                Metadata: [route]));
        }

        if (options.Value.BasePath is { HasValue: true } basePath && basePath.Value!.Trim('/') is { Length: > 0 } prefix)
        {
            entries.Add(new DefinedApiEntry(
                Id: "not-found",
                Source: SourceName,
                RoutePattern: "/" + prefix + "/{**path}",
                HttpMethods: [],
                Handler: static context =>
                {
                    context.Response.StatusCode = StatusCodes.Status404NotFound;
                    return Task.CompletedTask;
                },
                Order: int.MaxValue));
        }

        return entries;
    }
}

/// <summary>Reloads the tenant's workflow routes when the engine indexed a change to them.</summary>
public sealed class WorkflowHttpEndpointsInvalidator(IDefinedApiInvalidator invalidator) : IHttpEndpointRoutesObserver
{
    public void RoutesChanged() => invalidator.Invalidate(WorkflowHttpEndpointSource.SourceName);
}
