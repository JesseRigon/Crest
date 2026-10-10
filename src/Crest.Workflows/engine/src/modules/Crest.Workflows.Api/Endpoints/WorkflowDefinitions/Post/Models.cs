using Crest.Workflows.Api.Models;

namespace Crest.Workflows.Api.Endpoints.WorkflowDefinitions.Post;

internal record Response(LinkedWorkflowDefinitionModel WorkflowDefinition, bool AlreadyPublished, int ConsumingWorkflowCount);