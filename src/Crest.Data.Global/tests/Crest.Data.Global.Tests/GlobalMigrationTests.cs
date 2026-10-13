using Crest.Environment.Shell;
using Crest.Data.Global.Services;
using Crest.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;
using YesSql;

namespace Crest.Data.Global.Tests;

// Global migrations run on the platform's DataMigration convention (docs/queries.md › Crest.Data.Global
// and its own store): Create on a new store, the UpdateFrom chain after it, and the applied
// version recorded in the store, so a second start runs nothing.
public sealed class GlobalMigrationTests : IDisposable
{
    private readonly string _dataPath = Path.Combine(Path.GetTempPath(), "crest-global-" + Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task MigrationsRunOnceAndAreRecorded()
    {
        CountingMigration.Reset();

        var first = NewStore();
        await first.EnsureInitializedAsync(TestContext.Current.CancellationToken);
        first.Shutdown();

        Assert.Equal(1, CountingMigration.Creates);
        Assert.Equal(1, CountingMigration.Updates);

        var second = NewStore();
        await second.EnsureInitializedAsync(TestContext.Current.CancellationToken);
        var count = await second.ReadAsync((session, _) => session.Query<CountedDocument>().CountAsync(), TestContext.Current.CancellationToken);
        second.Shutdown();

        Assert.Equal(1, CountingMigration.Creates);
        Assert.Equal(1, CountingMigration.Updates);
        Assert.Equal(1, count);
    }

    private GlobalStore NewStore() => new(
        new ConfigurationBuilder().Build(),
        Options.Create(new ShellOptions { ShellsApplicationDataPath = _dataPath }),
        Options.Create(new DocumentJsonSerializerOptions()),
        NullLoggerFactory.Instance);

    public void Dispose()
    {
        if (Directory.Exists(_dataPath))
        {
            Directory.Delete(_dataPath, recursive: true);
        }
    }
}

public sealed class CountedDocument
{
    public long Id { get; set; }
}

public sealed class CountingMigration : GlobalDataMigration
{
    public static int Creates { get; private set; }

    public static int Updates { get; private set; }

    public static void Reset() => (Creates, Updates) = (0, 0);

    public async Task<int> CreateAsync()
    {
        Creates++;
        await Session.SaveAsync(new CountedDocument());
        return 1;
    }

    public int UpdateFrom1()
    {
        Updates++;
        return 2;
    }
}
