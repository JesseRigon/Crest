using Grove.Access;
using Grove.Environment.Shell;

namespace Grove.Workflows.Contexts;

/// <summary>Captures the acting user for a trigger: identity only, from the gated caller.</summary>
public interface IWorkflowUserContextAccessor
{
    WorkflowUserContext Capture();

    /// <summary>The captured actor sealed for a workflow input (<see cref="WorkflowActorSeal"/>).</summary>
    string CaptureSealed();
}

/// <summary>
/// The caller the access gate built for the current request or burst
/// (<see cref="ICallerContextAccessor.Current"/>) is the actor; with no gated caller the actor
/// is anonymous - the principal is never read here (docs/access.md › One place reads the
/// principal). A background entry point that wants to act as the system never captures it:
/// it runs a definition published as system.
/// </summary>
public sealed class WorkflowUserContextAccessor(ICallerContextAccessor callerContextAccessor, WorkflowActorSeal seal, ShellSettings shellSettings) : IWorkflowUserContextAccessor
{
    public WorkflowUserContext Capture() => callerContextAccessor.Current is { } caller
        ? WorkflowUserContext.From(caller)
        : WorkflowUserContext.Anonymous(shellSettings.Name);

    public string CaptureSealed() => seal.Seal(Capture());
}
