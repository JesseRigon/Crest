using Crest.Workflows.Abstractions;
using Crest.Workflows.Runtime;
using Crest.Workflows.Runtime.Entities;
using Crest.Workflows.Runtime.Filters;
using JetBrains.Annotations;

namespace Crest.Workflows.Api.Endpoints.ActivityExecutions.Get;

/// <summary>
/// Gets an individual execution for a given activity.
/// </summary>
[PublicAPI]
internal class Endpoint(IActivityExecutionStore store) : WorkflowsEndpointWithoutRequest<ActivityExecutionRecord>
{
    /// <inheritdoc />
    public override void Configure()
    {
        Get("/activity-executions/{id}");
        ConfigurePermissions("read:activity-execution");
    }

    /// <inheritdoc />
    public override async Task HandleAsync(CancellationToken cancellationToken)
    {
        var id = Route<string>("id");
        var filter = new ActivityExecutionRecordFilter
        {
            Id = id
        };

        var record = await store.FindAsync(filter, cancellationToken);

        if (record == null)
        {
            await Send.NotFoundAsync(cancellationToken);
            return;
        }

        await Send.OkAsync(record, cancellationToken);
    }
}