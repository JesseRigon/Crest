using Crest.Workflows.Mediator.Contracts;
using Crest.Workflows.Mediator.Models;
using Crest.Workflows.Models;
using JetBrains.Annotations;

namespace Crest.Workflows.Runtime.Commands;

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