using Grove.Workflows.Studio.Contracts;

namespace Grove.Workflows.Studio.Abstractions;

/// <summary>
/// A base class for modules.
/// </summary>
public abstract class FeatureBase : IFeature
{
    /// <inheritdoc />
    public virtual ValueTask InitializeAsync(CancellationToken cancellationToken = default) => ValueTask.CompletedTask;
}