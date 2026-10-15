using Grove.Workflows.Studio.Workflows.Models;

namespace Grove.Workflows.Studio.Workflows.Contracts;

/// A factory for creating <see cref="IWorkflowInstanceObserver"/> instances.
public interface IWorkflowInstanceObserverFactory
{
    /// Creates a new <see cref="IWorkflowInstanceObserver"/> instance.
    Task<IWorkflowInstanceObserver> CreateAsync(string workflowInstanceId);
    
    /// Creates a new <see cref="IWorkflowInstanceObserver"/> instance.
    Task<IWorkflowInstanceObserver> CreateAsync(WorkflowInstanceObserverContext context);
}