using Microsoft.Extensions.Options;
using Grove.Infrastructure.Settings;

namespace Grove.Communications.Email.Services;

public sealed class EmailOptionsConfiguration : IConfigureOptions<EmailOptions>
{
    private readonly ISiteService _siteService;
    private readonly IOptionsMonitor<EmailProviderOptions> _emailProviderOptions;
    private readonly IOptionsMonitor<EmailReceiveProviderOptions> _receiveProviderOptions;

    public EmailOptionsConfiguration(
        ISiteService siteService,
        IOptionsMonitor<EmailProviderOptions> emailProviderOptions,
        IOptionsMonitor<EmailReceiveProviderOptions> receiveProviderOptions)
    {
        _siteService = siteService;
        _emailProviderOptions = emailProviderOptions;
        _receiveProviderOptions = receiveProviderOptions;
    }

    public void Configure(EmailOptions options)
    {
        var emailSettings = _siteService.GetSettings<EmailSettings>();

        options.DefaultProviderName = Pick(
            emailSettings.DefaultProviderName,
            _emailProviderOptions.CurrentValue.Providers.ToDictionary(entry => entry.Key, entry => entry.Value.IsEnabled));

        options.DefaultReceiveProviderName = Pick(
            emailSettings.DefaultReceiveProviderName,
            _receiveProviderOptions.CurrentValue.Providers.ToDictionary(entry => entry.Key, entry => entry.Value.IsEnabled));
    }

    /// <summary>The chosen provider when it is enabled; otherwise the last enabled one (the last registered when none is).</summary>
    private static string Pick(string chosen, IReadOnlyDictionary<string, bool> providers)
    {
        if (!string.IsNullOrEmpty(chosen) && providers.TryGetValue(chosen, out var enabled) && enabled)
        {
            return chosen;
        }

        if (providers.Count == 0)
        {
            return null;
        }

        return providers.Where(entry => entry.Value).Select(entry => entry.Key).LastOrDefault()
            ?? providers.Keys.Last();
    }
}
