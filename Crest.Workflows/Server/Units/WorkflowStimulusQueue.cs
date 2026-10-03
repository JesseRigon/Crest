using Crest.Workflows.Runtime;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OrchardCore.Environment.Shell.Scope;

namespace Crest.Workflows.Units;

/// <summary>
/// Triggers fire after commit, never inside the raiser's transaction (plans/workflows.md ›
/// Posting on workflows). Every stimulus a registry, a stock event or a content event wants
/// to send goes through <see cref="WorkflowAfterCommit"/>: when the current shell scope has
/// committed its session, each is sent in a fresh child scope - one burst, one unit, per
/// stimulus, so the flows one stimulus starts never share a unit with another's. If the
/// session is cancelled or fails to commit - a failed unit - nothing is sent: a hooked flow
/// only ever sees committed state and no event exists for an invoice that never made it.
/// Outside a shell scope (unit tests) the send is immediate.
/// </summary>
public sealed class WorkflowStimulusQueue(IStimulusSender stimulusSender, WorkflowAfterCommit afterCommit, ILogger<WorkflowStimulusQueue> logger)
{
    public Task EnqueueAsync<TActivity>(object stimulus, StimulusMetadata metadata) where TActivity : IActivity =>
        EnqueueAsync(Helpers.ActivityTypeNameHelper.GenerateTypeName<TActivity>(), stimulus, metadata);

    public async Task EnqueueAsync(string activityTypeName, object stimulus, StimulusMetadata metadata)
    {
        // The event side's idempotency key: one id per raised stimulus, in the input of every
        // flow it starts or resumes.
        metadata.Input ??= new Dictionary<string, object>();
        metadata.Input.TryAdd(WorkflowsConstants.InputKeys.StimulusId, Guid.NewGuid().ToString("n"));

        var queued = afterCommit.Enqueue($"stimulus {activityTypeName} for {metadata.CorrelationId}", () =>
            // The parent scope activated the shell; a child never does (that path takes the
            // activation lock, which during setup is held by the scope this task belongs to).
            ShellScope.UsingChildScopeAsync(async child =>
            {
                // One unit per object at a time: the flows this stimulus starts about an object
                // run under that object's lock (the write side's serialization).
                await using var held = metadata.CorrelationId is { Length: > 0 } correlation
                    ? await child.ServiceProvider.GetRequiredService<IWorkflowObjectLock>().LockAsync(correlation)
                    : null;
                var result = await child.ServiceProvider.GetRequiredService<IStimulusSender>().SendAsync(activityTypeName, stimulus, metadata);
                logger.LogDebug("Stimulus {Activity} sent after commit for {Correlation}: {Count} workflow(s) started or resumed.", activityTypeName, metadata.CorrelationId, result.WorkflowInstanceResponses.Count);
            }, activateShell: false));

        if (!queued)
        {
            await stimulusSender.SendAsync(activityTypeName, stimulus, metadata);
        }
    }
}
