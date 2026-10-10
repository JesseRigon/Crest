using Crest.Workflows.Http.Bookmarks;
using Crest.Workflows.Activities;
using Crest.Workflows.Helpers;
using Crest.Workflows.Management.Models;
using Crest.Workflows.Runtime;
using Crest.Workflows.Runtime.Contracts;
using Crest.Workflows.Runtime.Entities;
using Crest.Workflows.Runtime.Filters;

namespace Crest.Workflows.Http.TriggerPayloadValidators;

public class HttpEndpointTriggerPayloadValidator(ITriggerStore triggerStore) : ITriggerPayloadValidator<HttpEndpointBookmarkPayload>
{
    public async Task ValidateAsync(
        HttpEndpointBookmarkPayload payload,
        Workflow workflow,
        StoredTrigger trigger,
        ICollection<WorkflowValidationError> validationErrors,
        CancellationToken cancellationToken)
    {
        var filter = new TriggerFilter
        {
            Name = ActivityTypeNameHelper.GenerateTypeName(typeof(HttpEndpoint))
        };
        var publishedWorkflowsTriggers = (await triggerStore.FindManyAsync(filter, cancellationToken)).ToList();

        var otherWorkflowsWithSamePath = publishedWorkflowsTriggers
            .Where(x =>
                x.WorkflowDefinitionId != workflow.Identity.DefinitionId &&
                x.Payload is HttpEndpointBookmarkPayload anotherHttpEndpointPayload &&
                anotherHttpEndpointPayload.Path == payload.Path &&
                anotherHttpEndpointPayload.Method == payload.Method)
            .ToList();

        if (otherWorkflowsWithSamePath.Count == 0)
            return;

        validationErrors.Add(new($"The {payload.Path} path and {payload.Method} method are already in use by another workflow!",
            trigger.ActivityId));
    }
}