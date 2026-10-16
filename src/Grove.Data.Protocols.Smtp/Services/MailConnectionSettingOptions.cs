using Grove.Data.Connectors;
using Grove.Infrastructure.Settings;

namespace Grove.Data.Protocols.Smtp.Services;

/// <summary>The tenant's connections of one protocol: the choices of a provider's connection field.</summary>
public abstract class MailConnectionSettingOptions(IConnectionRegistry registry, string protocol) : ISettingOptions
{
    public async Task<IReadOnlyList<SettingOption>> GetOptionsAsync(CancellationToken cancellationToken = default) =>
        [.. (await registry.ListAsync(cancellationToken))
            .Where(connection => string.Equals(connection.Protocol, protocol, StringComparison.OrdinalIgnoreCase))
            .OrderBy(connection => connection.DisplayName, StringComparer.CurrentCultureIgnoreCase)
            .Select(connection => new SettingOption(connection.Key, connection.DisplayName))];
}
