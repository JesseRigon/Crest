using Crest.Workflows.Api.Endpoints.ActivityExecutions;
using Crest.Workflows.Api.Endpoints.Descriptors;
using Crest.Workflows.Api.Endpoints.Platform;
using Crest.Workflows.Api.Endpoints.Resilience;
using Crest.Workflows.Api.Endpoints.Run;
using Crest.Workflows.Api.Endpoints.WorkflowDefinitions;
using Crest.Workflows.Api.Endpoints.WorkflowInstances;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Crest.Workflows.Api.Endpoints;

/// <summary>
/// The engine's management API: every area's minimal-API handlers under one route group, with
/// the body filter (<see cref="EngineBodyFilter"/>). The host declares the group (its prefix,
/// its antiforgery and exception filters) and calls <see cref="MapEngineApi"/> on it.
/// </summary>
public static class EngineApiEndpoints
{
    public static RouteGroupBuilder MapEngineApi(this IEndpointRouteBuilder routes, string prefix)
    {
        var group = routes.MapGroup(prefix);
        group.AddEndpointFilter<EngineBodyFilter>();

        DescriptorEndpoints.Map(group);
        ActivityExecutionEndpoints.Map(group);
        PlatformEndpoints.Map(group);
        RunEndpoints.Map(group);
        WorkflowDefinitionEndpoints.Map(group);
        WorkflowDefinitionExecuteEndpoints.Map(group);
        WorkflowDefinitionExportEndpoints.Map(group);
        WorkflowDefinitionImportEndpoints.Map(group);
        WorkflowInstanceEndpoints.Map(group);
        WorkflowInstanceExportEndpoints.Map(group);
        ResilienceEndpoints.Map(group);

        return group;
    }
}
