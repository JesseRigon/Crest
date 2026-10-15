using Grove.Workflows.Features.Abstractions;
using Grove.Workflows.Features.Services;
using Grove.Workflows.Api.RealTime.Handlers;
using Microsoft.Extensions.DependencyInjection;

namespace Grove.Workflows.Api.Features;

/// <summary>
/// Sets up a SignalR hub for receiving workflow events on the client.
/// </summary>
public class RealTimeWorkflowUpdatesFeature : FeatureBase
{
    /// <inheritdoc />
    public RealTimeWorkflowUpdatesFeature(IModule module) : base(module)
    {
    }

    /// <inheritdoc />
    public override void Apply()
    {
        Services.AddSignalR();
        Services.AddNotificationHandler<BroadcastWorkflowProgress>();
    }
}