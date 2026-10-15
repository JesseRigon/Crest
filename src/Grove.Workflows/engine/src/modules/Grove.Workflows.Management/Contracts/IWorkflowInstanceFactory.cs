using Grove.Workflows.Activities;
using Grove.Workflows.Management.Entities;
using Grove.Workflows.Management.Options;
using Grove.Workflows.State;

namespace Grove.Workflows.Management.Contracts;

/// <summary>
/// Creates new <see cref="WorkflowInstance"/> objects.
/// </summary>
public interface IWorkflowInstanceFactory
{
    /// <summary>
    /// Creates a new <see cref="WorkflowState"/> object.
    /// </summary>
    WorkflowState CreateWorkflowState(Workflow workflow, WorkflowInstanceOptions? options = null);

    /// <summary>
    /// Creates a new <see cref="WorkflowInstance"/> object.
    /// </summary>
    WorkflowInstance CreateWorkflowInstance(Workflow workflow, WorkflowInstanceOptions? options = null);
}