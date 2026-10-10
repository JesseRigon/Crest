using Crest.Workflows.Studio.Contracts;

namespace Crest.Workflows.Studio.Abstractions;

/// <summary>
/// A base class for modules.
/// </summary>
public abstract class FeatureBase : IFeature
{
    /// <inheritdoc />
    public virtual ValueTask InitializeAsync(CancellationToken cancellationToken = default) => ValueTask.CompletedTask;
}