using Grove.Workflows.Models;

namespace Grove.Workflows;

/// <summary>
/// Provides a way to modify activity descriptors as they are registered.
/// </summary>
public interface IActivityDescriptorModifier
{
    /// <summary>
    /// Modifies the specified activity descriptor.
    /// </summary>
    /// <param name="descriptor">The activity descriptor to modify.</param>
    void Modify(ActivityDescriptor descriptor);
}