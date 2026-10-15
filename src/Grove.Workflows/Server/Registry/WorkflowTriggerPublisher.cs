using Grove.Access;
using Grove.Workflows.Contexts;
using Grove.Workflows.Units;
using Microsoft.Extensions.Logging;

namespace Grove.Workflows.Registry;

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
    WorkflowActorSeal seal,
    ILogger<WorkflowTriggerPublisher> logger) : IWorkflowTriggerPublisher
{
    public Task PublishAsync(string triggerKey, string? correlationId, IDictionary<string, object>? payload = null, CancellationToken cancellationToken = default) =>
        PublishCoreAsync(triggerKey, correlationId, payload, actAs: null);

    public Task PublishForUserAsync(string triggerKey, string? correlationId, string userId, string? userName, IDictionary<string, object>? payload = null, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(userId);

        // A signed-in caller (an administrator creating a user) stays the actor; only the
        // anonymous request of a sign-in or a sign-up gives way to the user the event names.
        var current = userContext.Capture();
        if (current.IsAuthenticated || current.IsSystem)
        {
            return PublishCoreAsync(triggerKey, correlationId, payload, actAs: null);
        }

        // A member signing in on the portal names no organization yet: the run acts on the site side.
        var side = current.Side is CallerSide.Member or CallerSide.System ? CallerSide.Site : current.Side;
        return PublishCoreAsync(triggerKey, correlationId, payload, new WorkflowUserContext { UserId = userId, UserName = userName, Tenant = current.Tenant, Side = side });
    }

    private async Task PublishCoreAsync(string triggerKey, string? correlationId, IDictionary<string, object>? payload, WorkflowUserContext? actAs)
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
            [WorkflowsConstants.InputKeys.Actor] = actAs is null ? userContext.CaptureSealed() : seal.Seal(actAs),
        };

        await queue.EnqueueAsync<RegistryTrigger>(new RegistryTriggerStimulus(key), new() { CorrelationId = correlationId, Input = input }, actAs);
        logger.LogDebug("Workflow trigger {Trigger} queued for {Correlation}; it fires after commit.", key, correlationId);
    }
}
