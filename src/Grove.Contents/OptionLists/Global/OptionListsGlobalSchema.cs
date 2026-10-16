using System.Text.Json;
using Grove.Data.Global;
using YesSql;
using YesSql.Indexes;
using YesSql.Sql;

namespace Grove.Data.Global.Lists;

/// <summary>
/// Global-store schema for the shared lists: the index table and the data-file loader.
/// Fresh-install repeatable; re-runs replace Standard rows and keep Host rows.
/// </summary>
public sealed class OptionListsGlobalSchema : GlobalDataMigration
{
    public override string LegacySchemaName => "Grove.ContentPartLists.GlobalLists";

    public override IEnumerable<IIndexProvider> IndexProviders() => [new GlobalListIndexProvider()];

    public async Task<int> CreateAsync()
    {
        await SchemaBuilder.CreateMapIndexTableAsync<GlobalListIndex>(table => table
            .Column<string>(nameof(GlobalListIndex.Key), column => column.WithLength(128)));
        await SchemaBuilder.AlterIndexTableAsync<GlobalListIndex>(table => table
            .CreateIndex("IDX_GlobalListIndex_Key", "DocumentId", "Key"));

        await LoadAsync(Session, CancellationToken.None);
        return 1;
    }

    private static async Task LoadAsync(ISession session, CancellationToken cancellationToken)
    {
        var file = ReadData();
        var existing = (await session.Query<GlobalList, GlobalListIndex>().ListAsync(cancellationToken))
            .ToDictionary(list => list.Key, StringComparer.OrdinalIgnoreCase);

        foreach (var incoming in file.Lists)
        {
            foreach (var option in incoming.Options)
            {
                option.Source = GlobalOptionSources.Standard;
            }

            if (existing.TryGetValue(incoming.Key, out var current))
            {
                var host = current.Options.Where(option => option.Source == GlobalOptionSources.Host).ToList();
                current.DisplayText = incoming.DisplayText;
                current.DataLock = incoming.DataLock;
                current.Options = [.. incoming.Options, .. host];
                await session.SaveAsync(current);
            }
            else
            {
                await session.SaveAsync(incoming);
            }
        }
    }

    private static GlobalListsFile ReadData()
    {
        var assembly = typeof(OptionListsGlobalSchema).Assembly;
        var resourceName = assembly.GetManifestResourceNames().FirstOrDefault(name => IsDataFile(name, "global-lists.json"))
            ?? throw new InvalidOperationException("Embedded data file 'global-lists.json' is missing from Grove.Contents.");
        using var stream = assembly.GetManifestResourceStream(resourceName)!;
        return JsonSerializer.Deserialize<GlobalListsFile>(stream, new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
            ?? throw new InvalidOperationException("global-lists.json is empty.");
    }

    // Grove.Module.Targets embeds files with ">" as the folder separator
    // ("Grove.Data.Regions.Data>geo-nodes.json"); a plain SDK embed uses "."; match either.
    private static bool IsDataFile(string resourceName, string fileName) =>
        resourceName.EndsWith(">" + fileName, StringComparison.Ordinal)
        || resourceName.EndsWith("." + fileName, StringComparison.Ordinal)
        || string.Equals(resourceName, fileName, StringComparison.Ordinal);

    private sealed class GlobalListsFile
    {
        public int Version { get; set; }
        public List<GlobalList> Lists { get; set; } = [];
    }
}
