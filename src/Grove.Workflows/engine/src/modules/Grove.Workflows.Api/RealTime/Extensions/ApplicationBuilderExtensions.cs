using Grove.Workflows.Api.RealTime.Hubs;
using Microsoft.AspNetCore.Builder;

// ReSharper disable once CheckNamespace
namespace Grove.Workflows.Extensions;

/// <summary>
/// Provides extensions to <see cref="IApplicationBuilder"/>.
/// </summary>
public static class RealTimeApplicationBuilderExtensions
{
    /// <summary>
    /// Adds SignalR hubs for receiving workflow events on the client.
    /// </summary>
    public static IApplicationBuilder UseWorkflowsSignalRHubs(this IApplicationBuilder app) => app.UseEndpoints(endpoints => endpoints.MapHub<WorkflowInstanceHub>("/groveWorkflows/hubs/workflow-instance"));
}