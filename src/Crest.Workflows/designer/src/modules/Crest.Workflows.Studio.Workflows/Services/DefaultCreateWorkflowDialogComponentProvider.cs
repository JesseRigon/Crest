using Crest.Workflows.Studio.Workflows.Components.WorkflowDefinitionList;
using Crest.Workflows.Studio.Workflows.Contracts;

namespace Crest.Workflows.Studio.Workflows.Services;

/// <summary>
/// Provides the default implementation for the <see cref="ICreateWorkflowDialogComponentProvider"/> interface.
/// This implementation returns the type of the component used to display the dialog for creating a new workflow.
/// </summary>
public class DefaultCreateWorkflowDialogComponentProvider : ICreateWorkflowDialogComponentProvider
{
    /// <inheritdoc />
    public Type GetComponentType() => typeof(CreateWorkflowDialog);
}