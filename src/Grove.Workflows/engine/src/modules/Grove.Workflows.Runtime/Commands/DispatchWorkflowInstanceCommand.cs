using Grove.Workflows.Mediator.Contracts;
using Grove.Workflows.Mediator.Models;
using Grove.Workflows.Models;
using JetBrains.Annotations;

namespace Grove.Workflows.Runtime.Commands;

/// <summary>
/// A command to dispatch a workflow instance.
/// </summary>
[PublicAPI]
public class DispatchWorkflowInstanceCommand(string instanceId) : ICommand<Unit>
{
    public string InstanceId { get; init; } = instanceId;
    public string? BookmarkId { get; set; }
    public ActivityHandle? ActivityHandle { get; set; }
    public IDictionary<string, object>? Input { get; set; }
    public IDictionary<string, object>? Properties { get; set; }
    public string? CorrelationId { get; set; }
}