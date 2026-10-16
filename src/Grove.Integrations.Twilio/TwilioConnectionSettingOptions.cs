using Grove.Data.Connectors;
using Grove.Infrastructure.Settings;

namespace Grove.Integrations.Twilio;

/// <summary>The connections to Twilio's API: the declared one and any the tenant defined against it.</summary>
public sealed class TwilioConnectionSettingOptions(IConnectionRegistry registry) : ISettingOptions
{
    public async Task<IReadOnlyList<SettingOption>> GetOptionsAsync(CancellationToken cancellationToken = default) =>
        [.. (await registry.ListAsync(cancellationToken))
            .Where(connection => connection.FeatureId == TwilioConnection.FeatureId
                || (connection.BaseUrl?.StartsWith(TwilioConnection.BaseUrl, StringComparison.OrdinalIgnoreCase) ?? false))
            .OrderBy(connection => connection.DisplayName, StringComparer.CurrentCultureIgnoreCase)
            .Select(connection => new SettingOption(connection.Key, connection.DisplayName))];
}
