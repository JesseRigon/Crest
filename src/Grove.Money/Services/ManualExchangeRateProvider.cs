using Grove.Entities;
using Grove.Money.Abstractions;
using Grove.Money.Settings;
using Grove.Settings;

namespace Grove.Money.Services;

/// <summary>
/// The tenant's own rate table (<see cref="ExchangeRateSettings.Rates"/>), always registered at
/// the lowest priority: what a bought feed falls back to and what a tenant without one runs on.
/// </summary>
public sealed class ManualExchangeRateProvider(ISiteService siteService) : IExchangeRateProvider
{
    public const string ProviderName = "Manual";

    public string Name => ProviderName;

    public int Priority => int.MinValue;

    public async Task<ExchangeRate?> GetRateAsync(IDenomination from, IDenomination to, DateOnly date, CancellationToken cancellationToken = default)
    {
        var settings = (await siteService.GetSiteSettingsAsync()).GetOrCreate<ExchangeRateSettings>() ?? new ExchangeRateSettings();
        return Find(settings.Rates, from.Code, to.Code, date, Name);
    }

    /// <summary>The latest stated rate for the pair on or before the date, or the inverse of the latest stated reverse rate.</summary>
    public static ExchangeRate? Find(IEnumerable<ManualExchangeRate> rates, string from, string to, DateOnly date, string source)
    {
        var direct = rates
            .Where(rate => Is(rate.From, from) && Is(rate.To, to) && rate.EffectiveFrom <= date)
            .OrderByDescending(rate => rate.EffectiveFrom)
            .FirstOrDefault();
        if (direct is not null)
        {
            return new ExchangeRate(from, to, direct.Rate, direct.EffectiveFrom, source);
        }

        // The inverse of a stated rate is a stated rate.
        var inverse = rates
            .Where(rate => Is(rate.From, to) && Is(rate.To, from) && rate.EffectiveFrom <= date && rate.Rate != 0m)
            .OrderByDescending(rate => rate.EffectiveFrom)
            .FirstOrDefault();
        return inverse is null ? null : new ExchangeRate(from, to, 1m / inverse.Rate, inverse.EffectiveFrom, source);
    }

    private static bool Is(string a, string b) => string.Equals(a, b, StringComparison.OrdinalIgnoreCase);
}
