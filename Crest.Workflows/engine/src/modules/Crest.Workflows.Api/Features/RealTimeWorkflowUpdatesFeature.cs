using Crest.Workflows.Features.Abstractions;
using Crest.Workflows.Features.Services;
using Crest.Workflows.Api.RealTime.Handlers;
using Microsoft.Extensions.DependencyInjection;

namespace Crest.Workflows.Api.Features;

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