namespace Grove.Data.Localization.Data;

/// <summary>
/// A factory that creates instances of <see cref="NullDataLocalizer"/>.
/// This is used when the Grove.Data.Localization.Translations feature is not enabled.
/// </summary>
public sealed class NullDataLocalizerFactory : IDataLocalizerFactory
{
    /// <inheritdoc/>
    public IDataLocalizer Create() => NullDataLocalizer.Instance;
}
