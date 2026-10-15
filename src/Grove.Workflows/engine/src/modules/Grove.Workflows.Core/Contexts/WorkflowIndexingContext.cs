using Grove.Workflows.Expressions.Models;
using Grove.Workflows.Activities;

namespace Grove.Workflows;

public class WorkflowIndexingContext
{
    public WorkflowIndexingContext(Workflow workflow, CancellationToken cancellationToken)
    {
        Workflow = workflow;
        CancellationToken = cancellationToken;
    }

    public Workflow Workflow { get; }
    public IDictionary<ITrigger, MemoryRegister> Registers { get; } = new Dictionary<ITrigger, MemoryRegister>();
    public CancellationToken CancellationToken { get; }

    public MemoryRegister GetOrCreateRegister(ITrigger activity)
    {
        if (!Registers.TryGetValue(activity, out var register))
        {
            register = Workflow.CreateRegister();
            Registers[activity] = register;
        }

        return register;
    }
}