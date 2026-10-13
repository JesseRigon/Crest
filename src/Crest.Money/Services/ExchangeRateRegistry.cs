using Crest.Entities;
using Crest.Money.Abstractions;
using Crest.Money.Settings;
using Crest.Settings;
using Microsoft.Extensions.Logging;

namespace Crest.Money.Services;

/// <summary>
/// Walks the <see cref="IExchangeRateProvider"/>s by priority, highest first, with a per-scope
/// cache (a document with forty lines asks for the same pair forty times). A provider that
/// throws is logged and passed over; when nobody answers, the tenant's
/// <see cref="ExchangeRateSettings.FailureBehavior"/> decides between null and
/// <see cref="ExchangeRateUnavailableException"/>.
/// </summary>
public sealed class ExchangeRateRegistry(
    IEnumerable<IExchangeRateProvider> providers,
    IMoneyService money,
    ISiteService siteService,
    ILogger<ExchangeRateRegistry> logger) : IExchangeRateRegistry
{
    private readonly Dictionary<(string, string, string, string, DateOnly), ExchangeRate?> _cache = [];

    public Task<ExchangeRate?> GetRateAsync(string fromCurrency, string toCurrency, DateOnly date, CancellationToken cancellationToken = default)
    {
        var from = money.GetDenomination(DenominationKinds.Currency, fromCurrency);
        var to = money.GetDenomination(DenominationKinds.Currency, toCurrency);
        return from is null || to is null
            ? Task.FromResult<ExchangeRate?>(null)
            : GetRateAsync(from, to, date, cancellationToken);
    }

    public async Task<ExchangeRate?> GetRateAsync(IDenomination from, IDenomination to, DateOnly date, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(from);
        ArgumentNullException.ThrowIfNull(to);

        if (from.Equals(to))
        {
            return new ExchangeRate(from.Code, to.Code, 1m, date, ExchangeRate.IdentitySource);
        }

        var key = (from.Kind, from.Code.ToUpperInvariant(), to.Kind, to.Code.ToUpperInvariant(), date);
        if (_cache.TryGetValue(key, out var cached))
        {
            return cached;
        }

        string? failure = null;
        foreach (var provider in providers.OrderByDescending(provider => provider.Priority))
        {
            try
            {
                if (await provider.GetRateAsync(from, to, date, cancellationToken) is { } rate)
                {
                    _cache[key] = rate;
                    return rate;
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                // A failed feed is not a decline: recorded, and the walk continues to the
                // manual table beneath it.
                logger.LogError(ex, "Exchange rate provider {Provider} failed for {From}->{To} on {Date}.", provider.Name, from, to, date);
                failure ??= $"{provider.Name} failed: {ex.Message}";
            }
        }

        var settings = (await siteService.GetSiteSettingsAsync()).GetOrCreate<ExchangeRateSettings>() ?? new ExchangeRateSettings();
        if (string.Equals(settings.FailureBehavior, ExchangeRateFailureBehaviors.Block, StringComparison.OrdinalIgnoreCase))
        {
            throw new ExchangeRateUnavailableException(failure ?? $"No rate for {from.Code}->{to.Code} on {date:yyyy-MM-dd}.");
        }

        _cache[key] = null;
        return null;
    }
}
