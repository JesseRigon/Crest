using System.Reflection;
using System.Text.Json;
using Crest.Global;
using YesSql;
using YesSql.Indexes;
using YesSql.Sql;

namespace Crest.Money.Global;

/// <summary>ISO 4217 metadata, in the global store: the minor units Amount.GetRounded() rounds to are never a tenant setting (docs/money.md).</summary>
public sealed class CurrencyMetadata
{
    public long Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Symbol { get; set; } = string.Empty;
    public int MinorUnits { get; set; }
    public bool Enabled { get; set; } = true;
}

public sealed class CurrencyMetadataIndex : MapIndex
{
    public string Code { get; set; } = string.Empty;
}

public sealed class CurrencyMetadataIndexProvider : IndexProvider<CurrencyMetadata>
{
    public override void Describe(DescribeContext<CurrencyMetadata> context) =>
        context.For<CurrencyMetadataIndex>().Map(currency => new CurrencyMetadataIndex { Code = currency.Code });
}

/// <summary>Schema and loader for the currency table. Fresh-install repeatable: upserts by code from the shipped data file on version bump.</summary>
public sealed class CurrencyGlobalSchema : ICrestGlobalSchema
{
    public string Name => "Crest.Money.Currencies";
    public int Version => 1;

    public IEnumerable<IIndexProvider> IndexProviders() => [new CurrencyMetadataIndexProvider()];

    public async Task ApplyAsync(ICrestGlobalSchemaContext context, int fromVersion, CancellationToken cancellationToken)
    {
        if (fromVersion < 1)
        {
            await context.SchemaBuilder.CreateMapIndexTableAsync<CurrencyMetadataIndex>(table => table
                .Column<string>(nameof(CurrencyMetadataIndex.Code), column => column.WithLength(3)));
            await context.SchemaBuilder.AlterIndexTableAsync<CurrencyMetadataIndex>(table => table.CreateIndex("IDX_CurrencyMetadataIndex_Code", "DocumentId", "Code"));
        }

        var file = ReadData();
        var existing = (await context.Session.Query<CurrencyMetadata, CurrencyMetadataIndex>().ListAsync(cancellationToken))
            .ToDictionary(currency => currency.Code, StringComparer.Ordinal);
        foreach (var incoming in file.Currencies)
        {
            if (existing.TryGetValue(incoming.Code, out var current))
            {
                if (current.Name == incoming.Name && current.Symbol == incoming.Symbol && current.MinorUnits == incoming.MinorUnits)
                {
                    continue;
                }

                current.Name = incoming.Name;
                current.Symbol = incoming.Symbol;
                current.MinorUnits = incoming.MinorUnits;
                await context.Session.SaveAsync(current);
            }
            else
            {
                await context.Session.SaveAsync(incoming);
            }
        }
    }

    private static CurrenciesFile ReadData()
    {
        var assembly = typeof(CurrencyGlobalSchema).Assembly;
        var name = assembly.GetManifestResourceNames().FirstOrDefault(n => IsDataFile(n, "currencies.json"))
            ?? throw new InvalidOperationException("Embedded data file 'currencies.json' is missing from Crest.Money.");
        using var stream = assembly.GetManifestResourceStream(name)!;
        return JsonSerializer.Deserialize<CurrenciesFile>(stream, new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? new CurrenciesFile();
    }

    // Crest.Module.Targets embeds files with ">" as the folder separator; a plain SDK embed uses "."; match either.
    private static bool IsDataFile(string resourceName, string fileName) =>
        resourceName.EndsWith(">" + fileName, StringComparison.Ordinal)
        || resourceName.EndsWith("." + fileName, StringComparison.Ordinal)
        || string.Equals(resourceName, fileName, StringComparison.Ordinal);

    private sealed class CurrenciesFile
    {
        public int Version { get; set; }
        public List<CurrencyMetadata> Currencies { get; set; } = [];
    }
}
