using Crest.Money.Abstractions;
using Crest.Services;

namespace Crest.Money.Services;

/// <summary>
/// The <c>currency</c> option source: an option picker over the currency system. Currencies
/// are never a seeded list; <see cref="IMoneyService"/> and the <see cref="ICurrencyProvider"/>s
/// stay the owner and pickers read them through this provider. The ISO code is both the row
/// id and the key code compares.
/// </summary>
public sealed class CurrencyOptionSourceProvider(IEnumerable<ICurrencyProvider> currencyProviders) : IOptionSourceProvider
{
    public const string ProviderKey = "currency";

    public string Key => ProviderKey;

    public static class Columns
    {
        public const string IsoCode = "IsoCode";
        public const string EnglishName = "EnglishName";
        public const string NativeName = "NativeName";
        public const string Symbol = "Symbol";
        public const string DecimalPlaces = "DecimalPlaces";
    }

    public Task<IReadOnlyList<OptionSourceColumnDescriptor>> DescribeColumnsAsync(string qualifier, CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<OptionSourceColumnDescriptor>>(
        [
            new(Columns.IsoCode, "ISO code", IsDefaultDisplay: true),
            new(Columns.EnglishName, "Name"),
            new(Columns.NativeName, "Native name"),
            new(Columns.Symbol, "Symbol"),
            new(Columns.DecimalPlaces, "Decimal places"),
        ]);

    public Task<IReadOnlyList<OptionRow>> QueryAsync(OptionSourceQuery query, CancellationToken cancellationToken = default)
    {
        var rows = AllCurrencies().Select(currency => ToRow(currency, query.Columns));
        rows = OptionFilterMatcher.Apply(rows, query.Filters);
        rows = OptionFilterMatcher.Search(rows, query.SearchText,
            query.SearchColumns is { Count: > 0 } ? query.SearchColumns : [Columns.IsoCode, Columns.EnglishName]);
        rows = query.SortColumns is { Count: > 0 }
            ? OptionRowSorter.Apply(rows, query.SortColumns)
            : rows.OrderBy(row => row.Id, StringComparer.OrdinalIgnoreCase);
        return Task.FromResult<IReadOnlyList<OptionRow>>([.. rows.Skip(query.Skip).Take(query.Take)]);
    }

    public Task<IReadOnlyList<OptionRow>> GetByIdsAsync(string qualifier, IReadOnlyList<string> ids, IReadOnlyList<string> columns, CancellationToken cancellationToken = default)
    {
        if (ids.Count == 0)
        {
            return Task.FromResult<IReadOnlyList<OptionRow>>([]);
        }

        var wanted = new HashSet<string>(ids, StringComparer.OrdinalIgnoreCase);
        return Task.FromResult<IReadOnlyList<OptionRow>>(
            [.. AllCurrencies().Where(currency => wanted.Contains(currency.CurrencyIsoCode)).Select(currency => ToRow(currency, columns))]);
    }

    private IEnumerable<ICurrency> AllCurrencies() => currencyProviders
        .SelectMany(provider => provider.Currencies)
        .Where(currency => !string.IsNullOrWhiteSpace(currency?.CurrencyIsoCode))
        .DistinctBy(currency => currency.CurrencyIsoCode, StringComparer.OrdinalIgnoreCase);

    public static OptionRow ToRow(ICurrency currency, IReadOnlyList<string> columns)
    {
        var requested = columns is { Count: > 0 } ? columns : [Columns.IsoCode];
        var values = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
        foreach (var column in requested)
        {
            values[column] = column switch
            {
                Columns.IsoCode => currency.CurrencyIsoCode,
                Columns.EnglishName => currency.EnglishName,
                Columns.NativeName => currency.NativeName,
                Columns.Symbol => currency.Symbol,
                Columns.DecimalPlaces => currency.DecimalPlaces.ToString(System.Globalization.CultureInfo.InvariantCulture),
                _ => null,
            };
        }

        return new OptionRow(currency.CurrencyIsoCode, values) { Key = currency.CurrencyIsoCode };
    }
}
