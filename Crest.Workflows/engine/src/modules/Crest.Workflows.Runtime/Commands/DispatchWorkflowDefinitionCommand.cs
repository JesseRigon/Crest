using Crest.Workflows.Mediator.Contracts;
using Crest.Workflows.Mediator.Models;

namespace Crest.Workflows.Runtime.Commands;

/// <summary>
/// Dispatches a workflow definition.
/// </summary>
public class DispatchWorkflowDefinitionCommand(string definitionVersionId) : ICommand<Unit>
{
    public string DefinitionVersionId { get; init; } = definitionVersionId;
    public string? ParentWorkflowInstanceId { get; init; }
    public IDictionary<string, object>? Input { get; set; }
    public IDictionary<string, object>? Properties { get; set; }
    public string? CorrelationId { get; set; }
    public string? InstanceId { get; set; }
    public string? TriggerActivityId { get; set; }
}