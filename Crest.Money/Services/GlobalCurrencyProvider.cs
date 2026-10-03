using Crest.Global;
using Crest.Money.Global;
using Crest.Money;
using Crest.Money.Abstractions;
using OrchardCore.Modules;
using YesSql;

namespace Crest.Money.Services;

/// <summary>
/// The currency table as the global store holds it (plans/accounting.md › Currency model),
/// ahead of the culture-derived <see cref="CurrencyProvider"/> in provider order so a
/// super-tenant correction to minor units wins. ICurrencyProvider is synchronous, so the
/// table is loaded once per tenant activation and refreshed on the next activation - a
/// global edit reaches a tenant when it reloads, which is how every other global change
/// reaches it too.
/// </summary>
public sealed class GlobalCurrencyProvider : ICurrencyProvider
{
    private volatile Dictionary<string, ICurrency> _table = new(StringComparer.OrdinalIgnoreCase);

    public IEnumerable<ICurrency> Currencies => _table.Values;

    public ICurrency? GetCurrency(string isoCode) => isoCode is not null && _table.TryGetValue(isoCode, out var currency) ? currency : null;

    public bool IsKnownCurrency(string isoCode) => isoCode is not null && _table.ContainsKey(isoCode);

    public async Task LoadAsync(ICrestGlobalStore store, ICrestGlobalCache cache, CancellationToken cancellationToken)
    {
        var rows = await cache.GetOrCreateAsync<IReadOnlyList<CurrencyMetadata>>("accounting:currencies", async ct =>
            (await store.ReadAsync((session, innerCt) => session.Query<CurrencyMetadata, CurrencyMetadataIndex>().ListAsync(innerCt), ct)).ToArray(), cancellationToken);
        _table = rows
            .Where(row => row.Enabled)
            .ToDictionary(row => row.Code, row => (ICurrency)new Currency(row.Name, row.Name, row.Symbol, row.Code, row.MinorUnits), StringComparer.OrdinalIgnoreCase);
    }
}

/// <summary>Primes the tenant's <see cref="GlobalCurrencyProvider"/> when the shell activates.</summary>
public sealed class GlobalCurrencyProviderLoader(GlobalCurrencyProvider provider, ICrestGlobalStore store, ICrestGlobalCache cache) : ModularTenantEvents
{
    public override Task ActivatedAsync() => provider.LoadAsync(store, cache, CancellationToken.None);
}
