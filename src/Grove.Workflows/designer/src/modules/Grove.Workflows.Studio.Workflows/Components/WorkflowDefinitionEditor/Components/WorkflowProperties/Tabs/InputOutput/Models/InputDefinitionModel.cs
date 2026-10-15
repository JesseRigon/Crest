using Grove.Workflows.Api.Client.Resources.StorageDrivers.Models;
using Grove.Workflows.Api.Client.Resources.WorkflowDefinitions.Models;

namespace Grove.Workflows.Studio.Workflows.Components.WorkflowDefinitionEditor.Components.WorkflowProperties.Tabs.InputOutput.Models;

/// <summary>
/// Represents a input definition model.
/// </summary>
public class InputDefinitionModel : ArgumentDefinitionModel
{
    /// <summary>
    /// Gets or sets the uihint.
    /// </summary>
    public UIHintDescriptor? UIHint { get; set; }

    /// <summary>
    /// Gets or sets the storage driver.
    /// </summary>
    public StorageDriverDescriptor StorageDriver { get; set; } = default!;
}