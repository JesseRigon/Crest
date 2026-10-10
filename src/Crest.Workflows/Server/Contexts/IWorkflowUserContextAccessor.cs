using Crest.Access;
using Crest.Environment.Shell;
using Microsoft.AspNetCore.Http;

namespace Crest.Workflows.Contexts;

/// <summary>Captures the acting user for a trigger: identity only, from the gated caller when there is one.</summary>
public interface IWorkflowUserContextAccessor
{
    WorkflowUserContext Capture();
}

/// <summary>
/// The caller the access gate built for the current request or burst
/// (<see cref="ICallerContextAccessor.Current"/>) is the actor; outside a gated execution the
/// request's principal gives the identity (site side, since no bucket was stamped), and with
/// no request at all the actor is anonymous. A background entry point that wants to act as the
/// system never captures it: it runs a definition published as system.
/// </summary>
public sealed class WorkflowUserContextAccessor(ICallerContextAccessor callerContextAccessor, IHttpContextAccessor httpContextAccessor, ShellSettings shellSettings) : IWorkflowUserContextAccessor
{
    public WorkflowUserContext Capture() => callerContextAccessor.Current is { } caller
        ? WorkflowUserContext.From(caller)
        : WorkflowUserContext.From(httpContextAccessor.HttpContext?.User, shellSettings.Name);
}
