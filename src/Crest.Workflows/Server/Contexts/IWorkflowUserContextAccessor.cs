using Microsoft.AspNetCore.Http;
using Crest.Environment.Shell;

namespace Crest.Workflows.Contexts;

/// <summary>Captures the acting user for a trigger, from the current request when there is one.</summary>
public interface IWorkflowUserContextAccessor
{
    WorkflowUserContext Capture();
}

public sealed class WorkflowUserContextAccessor(IHttpContextAccessor httpContextAccessor, ShellSettings shellSettings) : IWorkflowUserContextAccessor
{
    public WorkflowUserContext Capture() => WorkflowUserContext.From(httpContextAccessor.HttpContext?.User, shellSettings.Name);
}
