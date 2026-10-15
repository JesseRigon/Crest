using Microsoft.Extensions.Options;
using Grove.Settings;

namespace Grove.Sms;

/// <summary>
/// The enabled SMS providers, by technical name: the choices of the default SMS provider.
/// </summary>
public sealed class SmsProviderSettingOptions(IOptionsMonitor<SmsProviderOptions> smsProviderOptions) : ISettingOptions
{
    public Task<IReadOnlyList<SettingOption>> GetOptionsAsync(CancellationToken cancellationToken = default)
    {
        IReadOnlyList<SettingOption> options = smsProviderOptions.CurrentValue.Providers
            .Where(entry => entry.Value.IsEnabled)
            .Select(entry => new SettingOption(entry.Key, entry.Key))
            .OrderBy(option => option.Text, StringComparer.Ordinal)
            .ToArray();

        return Task.FromResult(options);
    }
}
