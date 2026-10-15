using System.Runtime.CompilerServices;
using Grove.Workflows.Attributes;
using JetBrains.Annotations;

namespace Grove.Workflows.Activities;

/// <summary>
/// Mark the workflow as finished.
/// </summary>
[Activity("Grove.Workflows", "Primitives", "Mark the workflow as finished.")]
[PublicAPI]
public class Finish : CodeActivity, ITerminalNode
{
    /// <inheritdoc />
    public Finish([CallerFilePath] string? source = null, [CallerLineNumber] int? line = null) : base(source, line)
    {
    }

    /// <inheritdoc />
    protected override void Execute(ActivityExecutionContext context)
    {
        context.WorkflowExecutionContext.ClearCompletionCallbacks();
        context.WorkflowExecutionContext.Scheduler.Clear();
        context.WorkflowExecutionContext.Bookmarks.Clear();
        context.WorkflowExecutionContext.TransitionTo(WorkflowSubStatus.Finished);
    }
}