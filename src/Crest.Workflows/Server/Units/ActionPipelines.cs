namespace Crest.Workflows.Units;

/// <summary>
/// The two action pipelines of docs/operations.md › The four pipelines, named: what already
/// runs them in this module, so a reader of <c>CoreStartup</c> can tell which registration
/// belongs to which. Behaviour lives in the classes listed; this is the map.
/// </summary>
public static class ActionPipelines
{
    /// <summary>
    /// <b>Unit of work</b> - the synchronous action pipeline. One burst is one shell scope,
    /// one session, one transaction; inline hooks run as child instances inside it; a failed
    /// required attachment fails the unit and an API request answers 409; atomicity is derived,
    /// never asserted; per-object serialization by the object lock. Formed by
    /// <see cref="WorkflowUnitOfWork"/> (the unit's frames and failure),
    /// <see cref="UnitOfWorkCommitStateHandler"/> (the engine's commit as the unit's flush: a
    /// failed unit is discarded at commit and recorded in a fresh scope),
    /// <see cref="Hooks.WorkflowHookRunner"/> (attachments inside the unit),
    /// <see cref="WorkflowAtomicityAnalyzer"/> (what may be attached),
    /// <see cref="WorkflowObjectLock"/> (one unit per object at a time) and
    /// <see cref="Hooks.WorkflowHookFailedExceptionFilter"/> (the 409). The access gate
    /// (<see cref="Security.WorkflowAccessGateMiddleware"/>, <see cref="Security.ActivityAccessGateMiddleware"/>)
    /// runs inside every burst of both pipelines.
    /// </summary>
    public const string UnitOfWork = "unit-of-work";

    /// <summary>
    /// <b>Durable background</b> - the asynchronous action pipeline. The burst bookmarks and
    /// commits; the job is written in that unit, so it exists only if the unit committed; it
    /// runs afterwards in a child scope of its own with the connection's auth, resilience, rate
    /// limit and an idempotency key, and hands back by bookmark. The event side: stimuli after
    /// commit, partitioned by correlation id. Formed by <see cref="WorkflowAfterCommit"/>
    /// (after-commit emission; anything that must be after-commit uses it, never
    /// <c>WorkflowStateCommitted</c>), <see cref="WorkflowStimulusQueue"/> (stimuli queued to
    /// fire after commit), <see cref="DurableBackgroundActivityScheduler"/> and
    /// <see cref="ShellScopedBackgroundActivityInvoker"/> (the durable jobs and their hand-back),
    /// <see cref="ShellScopedBackgroundConsumers"/> (the engine's background channels in shell
    /// scopes), <see cref="ShellScopedRunScheduledTaskHandler"/> (timers, cron, delays) and
    /// <see cref="ShellScopedBookmarkQueueWorker"/> (queued resumes). Every entry point here
    /// runs as the caller the definition or the instance input names, never as an empty
    /// principal. Still to do (docs/workflows.md › Queues): partition by correlation id,
    /// idempotency keys on the event side.
    /// </summary>
    public const string DurableBackground = "durable-background";
}
