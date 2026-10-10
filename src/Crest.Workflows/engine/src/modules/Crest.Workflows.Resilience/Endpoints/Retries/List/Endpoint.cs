using Crest.Workflows.Abstractions;
using Crest.Workflows.Common.Models;
using Crest.Workflows.Models;
using Crest.Workflows.Resilience.Entities;

namespace Crest.Workflows.Resilience.Endpoints.Retries.List;

public class Endpoint(IRetryAttemptReader reader) : CrestWorkflowsEndpointWithoutRequest
{
    public override void Configure()
    {
        Get("/resilience/retries/{activityInstanceId}");
        ConfigurePermissions("read:*", "read:resilience", "read:resilience:retries");
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var skip = Query<int?>("skip", false);
        var take = Query<int?>("take", false);
        var pageArgs = skip == null && take == null ? null : PageArgs.FromRange(skip, take);
        var activityInstanceId = Route<string>("activityInstanceId");

        if (string.IsNullOrWhiteSpace(activityInstanceId))
        {
            AddError("ActivityInstanceId is required.");
            await Send.ErrorsAsync(cancellation: ct);
            return;
        }

        var page = await reader.ReadAttemptsAsync(activityInstanceId, pageArgs, ct);
        var response = new PagedListResponse<RetryAttemptRecord>(page);
        await Send.OkAsync(response, ct);
    }
}