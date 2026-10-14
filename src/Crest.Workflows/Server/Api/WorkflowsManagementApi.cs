using Crest.Api;
using Crest.Workflows.Api.Endpoints;
using Microsoft.AspNetCore.Routing;

namespace Crest.Workflows.Api;

/// <summary>
/// The workflow engine's management API (docs/MVC/api.md P7): the engine's minimal-API handlers
/// (<see cref="EngineApiEndpoints"/>) under <c>crest-workflows/api</c>, declared like any
/// module's API. The registry's group brings the antiforgery and exception filters; the engine's
/// <see cref="Security.ApiSecurityMiddleware"/> maps the caller onto each endpoint's policy.
/// </summary>
public sealed class WorkflowsManagementApi : ApiContributor
{
    public const string Prefix = "crest-workflows/api";

    public override string Name => "WorkflowsManagement";

    public override string FeatureId => "Crest.Workflows";

    // The engine's routes keep their own path, under the registry's group rather than its prefix.
    public override string RoutePrefix => string.Empty;

    public override void Map(IEndpointRouteBuilder group) => group.MapEngineApi(Prefix);
}
