using Crest.Workflows.Api.Client.Resources.WorkflowInstances.Models;

namespace Crest.Workflows.Api.Client.Resources.WorkflowDefinitions.Responses;

/// <summary>
/// A response to a request to execute a workflow definition.
/// </summary>
public record ExecuteWorkflowDefinitionResponse(WorkflowState WorkflowState);