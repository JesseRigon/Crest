namespace Grove.Icons;

using Grove.Iconify;

public sealed record IconProvidersSettings(IconifyIconProviderSettings Iconify)
{
    public static IconProvidersSettings Default { get; } = new(IconifyIconProviderSettings.Default);
}

public interface IIconProviderSettingsStore
{
    ValueTask<IconProvidersSettings> GetAsync(CancellationToken cancellationToken = default);

    ValueTask<IconProvidersSettings> SaveAsync(IconProvidersSettings settings, CancellationToken cancellationToken = default);
}
