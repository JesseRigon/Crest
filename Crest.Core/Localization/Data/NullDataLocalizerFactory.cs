namespace Crest.Localization.Data;

/// <summary>
/// A factory that creates instances of <see cref="NullDataLocalizer"/>.
/// This is used when the Crest.DataLocalization feature is not enabled.
/// </summary>
public sealed class NullDataLocalizerFactory : IDataLocalizerFactory
{
    /// <inheritdoc/>
    public IDataLocalizer Create() => NullDataLocalizer.Instance;
}
