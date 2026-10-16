using Grove.Communications.Email.Services;
using Grove.Infrastructure.Settings;
using Microsoft.Extensions.Options;

namespace Grove.Communications.Email;

/// <summary>The enabled send providers, by technical name: the choices of the default e-mail provider.</summary>
public sealed class EmailProviderSettingOptions(IOptionsMonitor<EmailProviderOptions> providerOptions) : ISettingOptions
{
    public Task<IReadOnlyList<SettingOption>> GetOptionsAsync(CancellationToken cancellationToken = default)
    {
        IReadOnlyList<SettingOption> options = providerOptions.CurrentValue.Providers
            .Where(entry => entry.Value.IsEnabled)
            .Select(entry => new SettingOption(entry.Key, entry.Key))
            .OrderBy(option => option.Text, StringComparer.Ordinal)
            .ToArray();

        return Task.FromResult(options);
    }
}

/// <summary>The enabled receive providers, by technical name: the choices of the default receive provider.</summary>
public sealed class EmailReceiveProviderSettingOptions(IOptionsMonitor<EmailReceiveProviderOptions> providerOptions) : ISettingOptions
{
    public Task<IReadOnlyList<SettingOption>> GetOptionsAsync(CancellationToken cancellationToken = default)
    {
        IReadOnlyList<SettingOption> options = providerOptions.CurrentValue.Providers
            .Where(entry => entry.Value.IsEnabled)
            .Select(entry => new SettingOption(entry.Key, entry.Key))
            .OrderBy(option => option.Text, StringComparer.Ordinal)
            .ToArray();

        return Task.FromResult(options);
    }
}
