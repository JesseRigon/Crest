#nullable enable
using System.Data.Common;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Grove.Access;
using Grove.Contents.Management;
using Grove.Contents.Management.Records;
using Grove.Data;
using Grove.Entities;
using Grove.Extensions;
using Grove.Json;
using Grove.Modules;
using Grove.Data.Queries;
using Grove.Data.Queries.Sql;
using Grove.Data.Queries.Sql.Models;
using YesSql;
using YesSql.Provider.Sqlite;
using YesSql.Serialization;
using YesSql.Sql;
using ISession = YesSql.ISession;

namespace Grove.Tests.Queries;

/// <summary>
/// Content items a query's rows point at load through the caller's scope (SQLite store): an id
/// outside the scope is never loaded, the order of the ids is kept; the Sql source pages and
/// counts, and reads document ids from a DocumentId column only.
/// </summary>
public class ScopedContentItemsTests : IAsyncLifetime
{
    private static readonly ScopeRule s_ownedByU1 = ScopeRule.Where(ScopeFilter.Of(ScopeCondition.Equal("Owner", "u1")));

    private IStore _store = null!;
    private string _tempFilename = null!;
    private long _a;
    private long _b;
    private long _c;

    public async ValueTask InitializeAsync()
    {
        _tempFilename = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        _store = await StoreFactory.CreateAndInitializeAsync(new Configuration().UseSqLite($"Data Source={_tempFilename};Cache=Shared"));

        var derivedOptions = new Mock<IOptions<JsonDerivedTypesOptions>>();
        derivedOptions.Setup(x => x.Value).Returns(new JsonDerivedTypesOptions());

        var jsonOptions = new DocumentJsonSerializerOptions();
        new DocumentJsonSerializerOptionsConfiguration(derivedOptions.Object).Configure(jsonOptions);

        var jsonSerializerOptions = new Mock<IOptions<DocumentJsonSerializerOptions>>();
        jsonSerializerOptions.Setup(x => x.Value).Returns(jsonOptions);

        _store.Configuration.ContentSerializer = new DefaultContentJsonSerializer(jsonSerializerOptions.Object);

        await using (var session = _store.CreateSession())
        {
            var builder = new SchemaBuilder(_store.Configuration, await session.BeginTransactionAsync());

            await builder.CreateMapIndexTableAsync<ContentItemIndex>(table => table
                .Column<string>("ContentItemId", c => c.WithLength(26))
                .Column<string>("ContentItemVersionId", c => c.WithLength(26))
                .Column<bool>("Latest")
                .Column<bool>("Published")
                .Column<string>("ContentType", column => column.WithLength(ContentItemIndex.MaxContentTypeSize))
                .Column<DateTime>("ModifiedUtc", column => column.Nullable())
                .Column<DateTime>("PublishedUtc", column => column.Nullable())
                .Column<DateTime>("CreatedUtc", column => column.Nullable())
                .Column<string>("Owner", column => column.Nullable().WithLength(ContentItemIndex.MaxOwnerSize))
                .Column<string>("Author", column => column.Nullable().WithLength(ContentItemIndex.MaxAuthorSize))
                .Column<string>("DisplayText", column => column.Nullable().WithLength(ContentItemIndex.MaxDisplayTextSize)));

            await session.SaveChangesAsync();
        }

        _store.RegisterIndexes<ContentItemIndexProvider>();

        await using (var session = _store.CreateSession())
        {
            var a = Item("a", "u1");
            var b = Item("b", "u2");
            var c = Item("c", "u1");

            await session.SaveAsync(a);
            await session.SaveAsync(b);
            await session.SaveAsync(c);
            await session.SaveChangesAsync();

            (_a, _b, _c) = (a.Id, b.Id, c.Id);
        }
    }

    public ValueTask DisposeAsync()
    {
        _store?.Dispose();

        try
        {
            File.Delete(_tempFilename);
        }
        catch (IOException)
        {
        }

        return ValueTask.CompletedTask;
    }

    private static ContentItem Item(string id, string owner) => new()
    {
        ContentItemId = id,
        ContentItemVersionId = id + "-v1",
        ContentType = "Blog",
        Owner = owner,
        Published = true,
        Latest = true,
    };

    [Fact]
    public async Task Load_DropsIdsOutsideTheScopeAndKeepsTheOrder()
    {
        await using var session = _store.CreateSession();

        var items = await ScopedContentItems.LoadAsync(session, [_c, 999_999, _b, _a], s_ownedByU1);

        Assert.Equal(["c", "a"], items.Select(item => item.ContentItemId));
    }

    [Fact]
    public async Task Load_AllRuleKeepsEveryExistingIdInOrder()
    {
        await using var session = _store.CreateSession();

        var items = await ScopedContentItems.LoadAsync(session, [_b, _c, _a], ScopeRule.All);

        Assert.Equal(["b", "c", "a"], items.Select(item => item.ContentItemId));
    }

    [Fact]
    public async Task Load_NoneRuleLoadsNothing()
    {
        await using var session = _store.CreateSession();

        Assert.Empty(await ScopedContentItems.LoadAsync(session, [_a, _b, _c], ScopeRule.None));
    }

    [Fact]
    public async Task LoadVersions_FiltersByScope()
    {
        await using var session = _store.CreateSession();

        var items = await ScopedContentItems.LoadVersionsAsync(session, ["b-v1", "a-v1", "missing"], s_ownedByU1);

        Assert.Equal(["a"], items.Select(item => item.ContentItemId));
    }

    [Fact]
    public async Task SqlSource_LiteralDocumentIdOutsideTheScopeLoadsNothing()
    {
        await using var session = _store.CreateSession();
        var source = SqlSource(session, TestScopes.Of(("ContentItemIndex", s_ownedByU1)));

        var results = await source.ExecuteQueryAsync(SqlQuery($"SELECT {_b} AS DocumentId", returnContentItems: true), new QueryRequest());

        Assert.Empty(results.Items);
    }

    [Fact]
    public async Task SqlSource_ContentItemsNeedADocumentIdColumn()
    {
        await using var session = _store.CreateSession();
        var source = SqlSource(session, TestScopes.Of(("ContentItemIndex", ScopeRule.All)));

        await Assert.ThrowsAsync<SqlQueryException>(() =>
            source.ExecuteQueryAsync(SqlQuery($"SELECT {_a} AS Id", returnContentItems: true), new QueryRequest()));
    }

    [Fact]
    public async Task SqlSource_PagesAndCounts()
    {
        await using var session = _store.CreateSession();
        var source = SqlSource(session, TestScopes.Of(("ContentItemIndex", ScopeRule.All)));
        var query = SqlQuery("SELECT ContentItemId FROM ContentItemIndex ORDER BY ContentItemId", returnContentItems: false);

        var first = await source.ExecuteQueryAsync(query, new QueryRequest { PageSize = 2 });

        Assert.Equal(3, first.Total);
        Assert.Equal(2, first.Items.Count());
        Assert.NotNull(first.NextPageToken);

        var second = await source.ExecuteQueryAsync(query, new QueryRequest { PageSize = 2, PageToken = first.NextPageToken });

        Assert.Single(second.Items);
        Assert.Null(second.NextPageToken);
    }

    [Fact]
    public async Task SqlSource_ParseFailureThrows()
    {
        await using var session = _store.CreateSession();
        var source = SqlSource(session, TestScopes.Of(("ContentItemIndex", ScopeRule.All)));

        var exception = await Assert.ThrowsAsync<SqlQueryException>(() =>
            source.ExecuteQueryAsync(SqlQuery("SELECT pg_read_file('x')", returnContentItems: false), new QueryRequest()));

        Assert.Contains(exception.Messages, message => message.Contains("pg_read_file", StringComparison.Ordinal));
    }

    [Fact]
    public async Task SqlSource_ParametersMatchIgnoringCase()
    {
        await using var session = _store.CreateSession();
        var source = SqlSource(session, TestScopes.Of(("ContentItemIndex", ScopeRule.All)));
        var query = SqlQuery("SELECT ContentItemId FROM ContentItemIndex WHERE Owner = @owner", returnContentItems: false);

        var results = await source.ExecuteQueryAsync(query, new QueryRequest { Parameters = new Dictionary<string, object> { ["OWNER"] = "u2" } });

        Assert.Equal(1, results.Total);
    }

    private static Query SqlQuery(string template, bool returnContentItems)
    {
        var query = new Query { Name = "q", Source = SqlQuerySource.SourceName, ReturnContentItems = returnContentItems };
        query.Put(new SqlQueryMetadata { Template = template });

        return query;
    }

    private SqlQuerySource SqlSource(ISession session, ScopeSet scopes)
    {
        var services = new ServiceCollection();
        services.AddSingleton<ICallerContextAccessor>(new CallerAccessor { Current = CallerContext.Anonymous("test", CallerSide.Site) });

        var provider = new Mock<IScopeSetProvider>();
        provider.Setup(p => p.GetAsync(It.IsAny<CallerContext>(), It.IsAny<CancellationToken>())).ReturnsAsync(scopes);
        services.AddSingleton(provider.Object);

        return new SqlQuerySource(new StoreConnections(_store), session, services.BuildServiceProvider(), NullLogger<SqlQuerySource>.Instance);
    }

    private sealed class StoreConnections(IStore store) : IDbConnectionAccessor
    {
        public DbConnection CreateConnection() => store.Configuration.ConnectionFactory.CreateConnection();
    }

    private sealed class CallerAccessor : ICallerContextAccessor
    {
        public CallerContext? Current { get; set; }
    }
}
