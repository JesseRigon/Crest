using Crest.Workflows.Contexts;
using Crest.Workflows.Units;
using Microsoft.Extensions.Logging;

namespace Crest.Workflows.Registry;

/// <summary>
/// Turns a registry's trigger into an engine stimulus, in this tenant, correlated to the
/// object, with the payload and the acting user as workflow input - queued to fire after the
/// current unit of work commits (<see cref="WorkflowStimulusQueue"/>). Refuses keys nobody
/// registered: a typo must fail loudly in a test, not fire nothing forever.
/// </summary>
public sealed class WorkflowTriggerPublisher(
    WorkflowRegistryCatalog catalog,
    WorkflowStimulusQueue queue,
    IWorkflowUserContextAccessor userContext,
    ILogger<WorkflowTriggerPublisher> logger) : IWorkflowTriggerPublisher
{
    public async Task PublishAsync(string triggerKey, string? correlationId, IDictionary<string, object>? payload = null, CancellationToken cancellationToken = default)
    {
        var key = triggerKey?.Trim().ToLowerInvariant() ?? throw new ArgumentNullException(nameof(triggerKey));
        if (catalog.FindTrigger(key) is null)
        {
            throw new InvalidOperationException($"'{triggerKey}' is not a registered workflow trigger. Register it with an IWorkflowTriggerProvider first.");
        }

        var input = new Dictionary<string, object>
        {
            [WorkflowsConstants.InputKeys.TriggerKey] = key,
            [WorkflowsConstants.InputKeys.Payload] = payload ?? new Dictionary<string, object>(),
            [WorkflowsConstants.InputKeys.Actor] = userContext.Capture(),
        };

        await queue.EnqueueAsync<RegistryTrigger>(new RegistryTriggerStimulus(key), new() { CorrelationId = correlationId, Input = input });
        logger.LogDebug("Workflow trigger {Trigger} queued for {Correlation}; it fires after commit.", key, correlationId);
    }
}
