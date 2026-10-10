using Microsoft.Extensions.Logging;
using Crest.Environment.Shell.Scope;

namespace Crest.Workflows.Units;

/// <summary>
/// Work that belongs after the current unit commits (docs/workflows.md › Posting on
/// workflows): sending stimuli, performing the external calls a flow recorded. Actions are
/// run by Crest's deferred task once the scope's session has committed, in order; when the
/// session is cancelled, fails to commit, or the unit failed, nothing runs - no event and no
/// call exists for state that never made it. Each action is expected to open its own child
/// scope so it is its own unit. Outside a shell scope (unit tests) <see cref="Enqueue"/> says
/// so and the caller acts immediately.
/// </summary>
public sealed class WorkflowAfterCommit(WorkflowUnitOfWork unit, ILogger<WorkflowAfterCommit> logger)
{
    private readonly List<(string Description, Func<Task> Action)> _actions = [];
    private bool _registered;
    private bool _committed;

    /// <summary>Queues the action; false when there is no shell scope to wait for.</summary>
    public bool Enqueue(string description, Func<Task> action)
    {
        if (ShellScope.Current is null)
        {
            return false;
        }

        _actions.Add((description, action));
        if (_registered)
        {
            return true;
        }

        _registered = true;
        // Runs after the session's own commit callback (registered first, so ours - 'last' -
        // follows); if that commit throws, this never runs and the deferred task does nothing.
        ShellScope.RegisterBeforeDispose(_ => { _committed = true; return Task.CompletedTask; }, last: true);
        ShellScope.AddDeferredTask(DrainAsync);
        return true;
    }

    private async Task DrainAsync(ShellScope scope)
    {
        if (!_committed || unit.IsFailed)
        {
            logger.LogDebug("{Count} after-commit action(s) dropped: the unit {Outcome}.", _actions.Count, unit.IsFailed ? "failed" : "did not commit");
            return;
        }

        foreach (var (description, action) in _actions)
        {
            try
            {
                await action();
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "After-commit action failed: {Description}.", description);
            }
        }
    }
}
