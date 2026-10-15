using Grove.Data.Types.Money;
using Grove.Data.Types.Money.Services;
using Xunit;

namespace Grove.Data.Types.Money.Tests;

// The currency option source over ICurrencyProvider (currencies are never a seeded
// list): row projection, the identity rule (ISO code is id AND key), and batched
// id resolution.
public class CurrencyOptionSourceProviderTests
{
    private static CurrencyOptionSourceProvider Provider() => new([new CurrencyProvider()]);

    [Fact]
    public void Rows_carry_iso_code_as_id_and_key()
    {
        var usd = new CurrencyProvider().GetCurrency("USD");
        var row = CurrencyOptionSourceProvider.ToRow(usd, [
            CurrencyOptionSourceProvider.Columns.IsoCode,
            CurrencyOptionSourceProvider.Columns.Symbol,
            CurrencyOptionSourceProvider.Columns.DecimalPlaces,
        ]);

        Assert.Equal("USD", row.Id);
        Assert.Equal("USD", row.Key);
        Assert.Equal("USD", row.Values[CurrencyOptionSourceProvider.Columns.IsoCode]);
        Assert.Equal("2", row.Values[CurrencyOptionSourceProvider.Columns.DecimalPlaces]);
        Assert.False(string.IsNullOrWhiteSpace(row.Values[CurrencyOptionSourceProvider.Columns.Symbol]));
    }

    [Fact]
    public async Task Describe_offers_iso_code_as_default_display()
    {
        var columns = await Provider().DescribeColumnsAsync("", TestContext.Current.CancellationToken);
        Assert.Contains(columns, column => column.Path == CurrencyOptionSourceProvider.Columns.IsoCode && column.IsDefaultDisplay);
    }

    [Fact]
    public async Task Query_searches_and_pages()
    {
        var rows = await Provider().QueryAsync(new Grove.Services.OptionSourceQuery(
            Qualifier: "",
            Columns: [CurrencyOptionSourceProvider.Columns.IsoCode, CurrencyOptionSourceProvider.Columns.EnglishName],
            SearchText: "USD",
            Take: 10), TestContext.Current.CancellationToken);

        Assert.Contains(rows, row => row.Id == "USD");
    }

    [Fact]
    public async Task Unknown_ids_are_absent_not_hollow()
    {
        var rows = await Provider().GetByIdsAsync("", ["USD", "XXQ"], [CurrencyOptionSourceProvider.Columns.IsoCode], TestContext.Current.CancellationToken);

        Assert.Single(rows);
        Assert.Equal("USD", rows[0].Id);
    }
}
