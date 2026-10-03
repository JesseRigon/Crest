using Crest.Workflows.Activities;
using Crest.Workflows.Management.Entities;
using Crest.Workflows.Management.Options;
using Crest.Workflows.State;

namespace Crest.Workflows.Management.Contracts;

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