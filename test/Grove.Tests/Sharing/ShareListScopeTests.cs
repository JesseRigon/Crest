#nullable enable
using System.Data.Common;
using Grove.Access;
using Grove.Data.Scoping;
using Grove.Queries;
using Grove.Queries.Sql;
using Grove.Queries.Structured;
using Grove.Sharing;
using Grove.Sharing.Indexes;
using Grove.Sharing.Migrations;
using Grove.Sharing.Services;
using YesSql;
using YesSql.Indexes;
using YesSql.Provider.Sqlite;
using YesSql.Sql;
using static Grove.Tests.Sharing.SharingWorld;

namespace Grove.Tests.Sharing;

/// <summary>The rows a list of the test kind reads: one per file.</summary>
public sealed class SharedFile
{
    public string FileId { get; set; } = string.Empty;
}

public sealed class SharedFileIndex : MapIndex
{
    public string FileId { get; set; } = string.Empty;
}

public sealed class SharedFileIndexProvider : IndexProvider<SharedFile>
{
    public override void Describe(DescribeContext<SharedFile> context) =>
        context.For<SharedFileIndex>().Map(file => new SharedFileIndex { FileId = file.FileId });
}

/// <summary>
/// Lists and search filter in the query (requirement 77), and agree with the decision on one
/// object (requirement 81). On a SQLite store holding the example drive's access index, for each
/// caller, the sharing scope compiled by each of the three compilers (YesSql expressions, the
/// SQL rewriter, the structured compiler) admits exactly the files the resolver lets them view,
/// at every instant around the time boundaries, before and after the job runs.
/// </summary>
public class ShareListScopeTests : IAsyncLifetime
{
    private IStore _store = null!;
    private string _file = null!;

    public async ValueTask InitializeAsync()
    {
        _file = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        _store = await StoreFactory.CreateAndInitializeAsync(new Configuration().UseSqLite($"Data Source={_file};Cache=Shared"));

        await using (var session = _store.CreateSession())
        {
            var builder = new SchemaBuilder(_store.Configuration, await session.BeginTransactionAsync());
            await new SharingMigrations { SchemaBuilder = builder }.CreateAsync();
            await builder.CreateMapIndexTableAsync<SharedFileIndex>(table => table.Column<string>(nameof(SharedFileIndex.FileId), column => column.WithLength(255)));
            await session.SaveChangesAsync();
        }

        _store.RegisterIndexes<ShareAccessIndexProvider>();
        _store.RegisterIndexes<ShareRecalculationIndexProvider>();
        _store.RegisterIndexes<SharedFileIndexProvider>();
    }

    public ValueTask DisposeAsync()
    {
        _store?.Dispose();
        try
        {
            File.Delete(_file);
        }
        catch (IOException)
        {
        }

        return ValueTask.CompletedTask;
    }

    private static readonly CallerContext[] s_callers =
    [
        Visitor,
        User("cora", "Customer"),
        User("cara", "customer"),
        User("sam", "Staff"),
        User("mia", "Marketing"),
        User("dana"),
        User("cs", "Customer", "Staff"),
        User("cm", "Customer", "Marketing"),
        User("eve", "Staff"),
        User("tia"),
        Member("mo", "acme", "Customer"),
        Member("al", "acme", "MemberAdmin"),
        Member("bo", "beta", "MemberAdmin"),
        User("ms", "Marketing", "Staff"),
    ];

    private async Task IndexAsync(SharingWorld world)
    {
        await using var session = _store.CreateSession();
        foreach (var id in world.Files.Nodes.Keys)
        {
            await session.SaveAsync(new SharedFile { FileId = id });
        }

        await world.IndexAllAsync(new YesSqlShareAccessStore(session));
        await session.SaveChangesAsync();
    }

    private async Task<HashSet<string>> AllowedAsync(SharingWorld world, CallerContext caller, AccessLevel minimum = AccessLevel.Viewer)
    {
        var resolver = world.Resolver();
        var allowed = new HashSet<string>(StringComparer.Ordinal);
        foreach (var id in world.Files.Nodes.Keys)
        {
            if ((await resolver.ResolveAsync(caller, Ref(id), TestContext.Current.CancellationToken)).Allows(minimum))
            {
                allowed.Add(id);
            }
        }

        return allowed;
    }

    private static ScopeRule Scope(SharingWorld world, CallerContext caller, AccessLevel minimum = AccessLevel.Viewer) =>
        new SharingScopeProvider(SharingWorld.Kind, world.Files.ScopeTarget, world.Clock).Build(caller, minimum);

    /// <summary>The files the scope admits, through YesSql's expression compiler.</summary>
    private async Task<HashSet<string>> ListedByExpressionAsync(ScopeRule rule)
    {
        await using var session = _store.CreateSession();
        var rows = await session.QueryIndex<SharedFileIndex>(ScopeExpressions.ToPredicate<SharedFileIndex>(rule)).ListAsync();
        return new HashSet<string>(rows.Select(row => row.FileId), StringComparer.Ordinal);
    }

    /// <summary>The files the scope admits, through the SQL rewriter.</summary>
    private async Task<HashSet<string>> ListedBySqlAsync(ScopeRule rule)
    {
        var parameters = new Dictionary<string, object>();
        var parsed = SqlParser.TryParse(
            "select FileId from SharedFileIndex",
            string.Empty,
            _store.Configuration.SqlDialect,
            _store.Configuration.TablePrefix ?? string.Empty,
            parameters,
            new ScopeSet("test", new Dictionary<string, ScopeRule>(StringComparer.OrdinalIgnoreCase) { [nameof(SharedFileIndex)] = rule }),
            out var sql,
            out var messages);
        Assert.True(parsed, string.Join(' ', messages));
        return await ExecuteAsync(sql, parameters);
    }

    /// <summary>The files the scope admits, through the structured compiler.</summary>
    private async Task<HashSet<string>> ListedByStructuredAsync(ScopeRule rule)
    {
        var query = new StructuredQuery
        {
            Steps =
            [
                new FromStep { Source = nameof(SharedFileIndex), Alias = "f" },
                new ProjectStep { Columns = [new ProjectedColumn(nameof(SharedFileIndex.FileId))] },
                new PageStep { Size = StructuredQueryCompiler.MaxPageSize },
            ],
        };

        var statement = StructuredQueryCompiler.Compile(
            QueryPlan.Build(query, new FileCatalog()),
            _store.Configuration.SqlDialect,
            _store.Configuration.TablePrefix ?? string.Empty,
            string.Empty,
            new ScopeSet("test", new Dictionary<string, ScopeRule>(StringComparer.OrdinalIgnoreCase) { [nameof(SharedFileIndex)] = rule }),
            null,
            null,
            null);
        return await ExecuteAsync(statement.Sql, statement.Parameters);
    }

    private async Task<HashSet<string>> ExecuteAsync(string sql, IReadOnlyDictionary<string, object> parameters)
    {
        await using DbConnection connection = _store.Configuration.ConnectionFactory.CreateConnection();
        await connection.OpenAsync(TestContext.Current.CancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        foreach (var (name, value) in parameters)
        {
            var parameter = command.CreateParameter();
            parameter.ParameterName = "@" + name;
            parameter.Value = value ?? DBNull.Value;
            command.Parameters.Add(parameter);
        }

        var listed = new HashSet<string>(StringComparer.Ordinal);
        await using var reader = await command.ExecuteReaderAsync(TestContext.Current.CancellationToken);
        while (await reader.ReadAsync(TestContext.Current.CancellationToken))
        {
            listed.Add(reader.GetString(0));
        }

        return listed;
    }

    private sealed class FileCatalog : IIndexTableCatalog
    {
        public IReadOnlyList<IndexTableDescriptor> Tables { get; } =
            [new(nameof(SharedFileIndex), typeof(SharedFileIndex), IndexTableCatalog.ColumnsOf(typeof(SharedFileIndex)))];

        public IndexTableDescriptor? Find(string name) =>
            Tables.FirstOrDefault(table => string.Equals(table.Name, name, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>Every caller's list, by every compiler, is exactly what the resolver allows.</summary>
    private async Task AssertConsistentAsync(SharingWorld world, string moment, AccessLevel minimum = AccessLevel.Viewer)
    {
        foreach (var caller in s_callers)
        {
            var expected = await AllowedAsync(world, caller, minimum);
            var rule = Scope(world, caller, minimum);
            if (rule.Kind == ScopeKind.None)
            {
                Assert.Empty(expected);
                continue;
            }

            var who = $"{caller.UserId ?? "visitor"} {moment}";

            Assert.True(expected.SetEquals(await ListedByExpressionAsync(rule)), $"YesSql expression list differs for {who}.");
            Assert.True(expected.SetEquals(await ListedBySqlAsync(rule)), $"SQL list differs for {who}.");
            Assert.True(expected.SetEquals(await ListedByStructuredAsync(rule)), $"Structured list differs for {who}.");
        }
    }

    [Fact]
    public async Task EachCallersListIsExactlyWhatTheResolverAllows()
    {
        var world = Example();
        await IndexAsync(world);

        await AssertConsistentAsync(world, "in the example");

        // The example, read off the lists.
        Assert.Equal(new[] { "chair-final.jpg" }, await ListedByExpressionAsync(Scope(world, Visitor)));
        Assert.Equal(new[] { "chair-final.jpg" }, await ListedByExpressionAsync(Scope(world, User("sam", "Staff"))));
        Assert.DoesNotContain("chair.psd", await ListedByExpressionAsync(Scope(world, User("cora", "Customer"))));
        Assert.DoesNotContain("chair.psd", await ListedByExpressionAsync(Scope(world, User("cs", "Customer", "Staff"))));
        Assert.Contains("chair.psd", await ListedByExpressionAsync(Scope(world, User("cm", "Customer", "Marketing"))));
        Assert.Equal(new[] { "chair-final.jpg", "chair.psd", "spec-sheets", "spec.psd" }, (await ListedByExpressionAsync(Scope(world, User("dana")))).Order(StringComparer.Ordinal));
    }

    [Fact]
    public async Task ListsAtHigherLevelsAndOrganizationScopedRolesAgreeWithTheResolver()
    {
        var world = Example();

        // Within the role tier a deeper Viewer row caps a shallower Editor grant; at one depth the
        // grants stack; an organization-scoped role reaches only its organization.
        world.Share("product-info", ShareEntry.Grant(SharePrincipal.Role("Staff"), AccessLevel.Editor));
        world.Share("images", ShareEntry.Grant(SharePrincipal.Role("Staff"), AccessLevel.Viewer));
        world.Share("spec-sheets", ShareEntry.Grant(SharePrincipal.Role("MemberAdmin", "acme"), AccessLevel.Editor));
        world.Share("chair-original.jpg", ShareEntry.Grant(SharePrincipal.Role("Marketing"), AccessLevel.Viewer), ShareEntry.Grant(SharePrincipal.Role("Staff"), AccessLevel.Manager));
        await IndexAsync(world);

        await AssertConsistentAsync(world, "at Viewer");
        await AssertConsistentAsync(world, "at Editor", AccessLevel.Editor);
        await AssertConsistentAsync(world, "at Manager", AccessLevel.Manager);

        Assert.Contains("spec.psd", await ListedByExpressionAsync(Scope(world, Member("al", "acme", "MemberAdmin"), AccessLevel.Editor)));
        // Another organization's member keeps only Spec sheets' tenant Viewer (requirement 37).
        Assert.DoesNotContain("spec.psd", await ListedByExpressionAsync(Scope(world, Member("bo", "beta", "MemberAdmin"), AccessLevel.Editor)));
    }

    [Fact]
    public async Task TheTenantAdministratorListsEverything()
    {
        var world = Example();
        await IndexAsync(world);
        var admin = new CallerContext { Tenant = "Default", Side = CallerSide.Admin, UserId = "root", UserName = "root", IsSuperUser = true };

        Assert.Same(ScopeRule.All, Scope(world, admin));
    }

    [Fact]
    public async Task AnObjectNeverIndexedIsInNoList()
    {
        var world = Example();
        await IndexAsync(world);
        world.Files.Add("late.jpg", "images", extension: "jpg", tag: "final");
        await using (var session = _store.CreateSession())
        {
            await session.SaveAsync(new SharedFile { FileId = "late.jpg" });
            await session.SaveChangesAsync();
        }

        Assert.DoesNotContain("late.jpg", await ListedByExpressionAsync(Scope(world, Visitor)));
    }

    [Fact]
    public async Task ListsFollowTimeBoundariesBetweenRunsOfTheJob()
    {
        var world = Example();
        var now = new DateTimeOffset(world.Clock.UtcNow, TimeSpan.Zero);
        var ends = now.AddHours(1);
        world.Share("product-info", ShareEntry.Grant(SharePrincipal.User("eve"), AccessLevel.Viewer));
        world.Share("images", ShareEntry.Block(SharePrincipal.User("eve"), ends: ends));
        // Tia's own window decides Spec sheets for her: without the example's tenant grant there
        // (every signed-in user would see it through that, requirement 37).
        world.Store.Documents[Ref("spec-sheets")].Entries.RemoveAll(entry => entry.Principal.Kind == SharePrincipalKind.Tenant);
        world.Share("spec-sheets", ShareEntry.Grant(SharePrincipal.User("tia"), AccessLevel.Viewer, starts: now.AddMinutes(30), ends: ends));
        world.Share("chair-original.jpg", ShareEntry.Block(SharePrincipal.Role("Staff"), starts: now.AddMinutes(10)));
        await IndexAsync(world);

        await AssertConsistentAsync(world, "now");
        Assert.DoesNotContain("spec-sheets", await ListedByExpressionAsync(Scope(world, User("tia"))));
        Assert.DoesNotContain("images", await ListedByExpressionAsync(Scope(world, User("eve", "Staff"))));

        // No job has run: the rows' windows decide.
        foreach (var minutes in new[] { 10, 31, 59, 60, 61, 120 })
        {
            world.Clock.UtcNow = now.AddMinutes(minutes).UtcDateTime;
            await AssertConsistentAsync(world, $"at +{minutes} min");
        }

        Assert.Contains("images", await ListedByExpressionAsync(Scope(world, User("eve", "Staff"))));
        Assert.DoesNotContain("spec-sheets", await ListedByExpressionAsync(Scope(world, User("tia"))));

        // The job recalculates what is due; the lists stay the same.
        await using (var session = _store.CreateSession())
        {
            Assert.True(await world.Indexer(new YesSqlShareAccessStore(session)).RecalculateDueAsync(100, TestContext.Current.CancellationToken) > 0);
            await session.SaveChangesAsync();
        }

        await AssertConsistentAsync(world, "after the job");
    }

    [Fact]
    public async Task VerificationAgreesWithTheStoredIndex()
    {
        var world = Example();
        await IndexAsync(world);

        await using var session = _store.CreateSession();
        var indexer = world.Indexer(new YesSqlShareAccessStore(session));
        foreach (var id in world.Files.Nodes.Keys)
        {
            Assert.Empty(await indexer.VerifyAsync(Ref(id), TestContext.Current.CancellationToken));
        }

        // A change the index was not told about is found.
        world.Share("spec-sheets", ShareEntry.Grant(SharePrincipal.User("zed"), AccessLevel.Viewer));
        Assert.NotEmpty(await indexer.VerifyAsync(Ref("spec-sheets"), TestContext.Current.CancellationToken));
    }
}
