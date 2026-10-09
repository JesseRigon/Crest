using Crest.Workflows.CommitStates;
using Crest.Workflows.Models;
using Crest.Workflows.State;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Crest.Data.Documents;
using Crest.Environment.Shell.Scope;

namespace Crest.Workflows.Units;

/// <summary>
/// The unit of work a burst of workflow execution runs in (docs/workflows.md › Posting on
/// workflows): one shell scope, one YesSql session, committed as one transaction when the
/// scope ends - no store commits mid-run. A required hook attachment that fails, or a
/// <c>Fail unit</c> activity, marks the unit failed; at commit time every write of the burst
/// is discarded and the faulted state is recorded in a fresh scope so the journal still
/// says what happened. The unit is not only a workflow's: a service running a hook slot
/// inside its own request (Accounting creating an invoice) fails the same unit, and the
/// scope's session is cancelled before Crest would commit it.
/// </summary>
public sealed class WorkflowUnitOfWork(IDocumentStore? documentStore = null)
{
    private readonly Stack<Frame> _frames = new();
    private readonly Frame _root = new();
    private bool _cancelRegistered;

    /// <summary>The whole unit failed: the burst is discarded at commit.</summary>
    public bool IsFailed => _root.IsFailed;
    public string? Reason => _root.Reason;
    public string? ActivityId => _root.ActivityId;

    /// <summary>Fails the innermost frame: the unit itself, or the hook attachment currently running inside it.</summary>
    public void Fail(string reason, string? activityId = null)
    {
        if (_frames.Count > 0)
        {
            _frames.Peek().Fail(reason, activityId);
            return;
        }

        _root.Fail(reason, activityId);
        if (_cancelRegistered || documentStore is null || ShellScope.Current is null)
        {
            return;
        }

        // Before-dispose callbacks registered without 'last' run ahead of the session's own
        // commit callback, so the cancelled session never commits whoever ends the scope.
        _cancelRegistered = true;
        ShellScope.RegisterBeforeDispose(_ => documentStore.CancelAsync());
    }

    /// <summary>
    /// A hook attachment runs in a frame of its own: its failure is the frame's until the
    /// Hook decides - a required attachment fails the unit, a best-effort one is only noted.
    /// </summary>
    public Frame Enter()
    {
        var frame = new Frame(this);
        _frames.Push(frame);
        return frame;
    }

    public sealed class Frame : IDisposable
    {
        private readonly WorkflowUnitOfWork? _owner;

        internal Frame() { }
        internal Frame(WorkflowUnitOfWork owner) => _owner = owner;

        public bool IsFailed { get; private set; }
        public string? Reason { get; private set; }
        public string? ActivityId { get; private set; }

        internal void Fail(string reason, string? activityId)
        {
            if (IsFailed) return;
            IsFailed = true;
            Reason = reason;
            ActivityId = activityId;
        }

        public void Dispose()
        {
            if (_owner is not null && _owner._frames.Count > 0 && ReferenceEquals(_owner._frames.Peek(), this))
            {
                _owner._frames.Pop();
            }
        }
    }
}

/// <summary>Thrown by an activity to fail the unit it runs in; the engine records it as the workflow's incident.</summary>
public sealed class WorkflowUnitFailedException(string message) : Exception(message);

/// <summary>
/// Wraps the engine's commit handler. A healthy burst commits through the engine as usual
/// (its writes land in the scope's session and commit with it). A failed burst is cancelled
/// - the document store discards everything written in this scope, business and engine state
/// alike - and the workflow's faulted state is then persisted in a child shell scope with its
/// own session, so the instance shows as Faulted with the reason while nothing it did stands.
/// </summary>
public sealed class UnitOfWorkCommitStateHandler(ICommitStateHandler inner, WorkflowUnitOfWork unit, IDocumentStore documentStore, ILogger<UnitOfWorkCommitStateHandler> logger) : ICommitStateHandler
{
    public async Task CommitAsync(WorkflowExecutionContext workflowExecutionContext, CancellationToken cancellationToken = default)
    {
        if (!unit.IsFailed)
        {
            await inner.CommitAsync(workflowExecutionContext, cancellationToken);
            return;
        }

        await DiscardAndRecordAsync(workflowExecutionContext, null, cancellationToken);
    }

    public async Task CommitAsync(WorkflowExecutionContext workflowExecutionContext, WorkflowState workflowState, CancellationToken cancellationToken = default)
    {
        if (!unit.IsFailed)
        {
            await inner.CommitAsync(workflowExecutionContext, workflowState, cancellationToken);
            return;
        }

        await DiscardAndRecordAsync(workflowExecutionContext, workflowState, cancellationToken);
    }

    private async Task DiscardAndRecordAsync(WorkflowExecutionContext context, WorkflowState? state, CancellationToken cancellationToken)
    {
        _ = state; // re-extracted in the recording scope
        logger.LogWarning("Workflow unit failed in instance {Instance}: {Reason}. Discarding the burst's writes.", context.Id, unit.Reason);
        await documentStore.CancelAsync();

        // The burst's bookmarks (a background activity's, a wait's) belong to a run that never
        // happened: a faulted instance must not leave bookmarks behind for stimuli to resume.
        context.Bookmarks = new List<Crest.Workflows.Models.Bookmark>();

        // The unit is failed only by throwing (the Hook activity, Fail unit), so the engine has
        // already faulted the workflow and recorded the incident. The record goes through the
        // engine's own handler in a scope whose session is not the cancelled one; the handler
        // resolved there is this type again, but that scope's unit is clean, so it commits.
        await ShellScope.UsingChildScopeAsync(scope => scope.ServiceProvider.GetRequiredService<ICommitStateHandler>().CommitAsync(context, cancellationToken), activateShell: false);
    }
}
