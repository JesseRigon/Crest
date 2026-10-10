using Crest.Workflows.Api.Client.Resources.WorkflowInstances.Enums;

namespace Crest.Workflows.Api.Client.Resources.WorkflowInstances.Responses;

/// <summary>
/// Represents the response containing the last updated timestamp of a workflow instance.
/// </summary>
public record WorkflowInstanceExecutionStateResponse(WorkflowStatus Status, WorkflowSubStatus WorkflowSubStatus, DateTimeOffset UpdatedAt);