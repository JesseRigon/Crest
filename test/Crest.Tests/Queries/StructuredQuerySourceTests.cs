#nullable enable
using System.Text.Json.Nodes;
using Crest.Access;
using Crest.Data;
using Crest.Entities;
using Crest.Queries;
using Crest.Queries.Structured;
using YesSql.Provider.Sqlite;

namespace Crest.Tests.Queries;

public class StructuredQuerySourceTests
{
    private static Query SavedQuery()
    {
        var query = new Query { Name = "posts", Source = StructuredQuerySource.SourceName };
        query.Put(new StructuredQueryMetadata
        {
            Definition = new StructuredQuery
            {
                Steps = [new FromStep { Source = "ContentItemIndex" }, new ProjectStep { Columns = [new ProjectedColumn("ContentItemId")] }],
            },
        });

        return query;
    }

    private static StructuredQuerySource Source(ScopeSet? scopes, bool withCaller = true)
    {
        var configuration = new Mock<YesSql.IConfiguration>();
        configuration.SetupGet(c => c.SqlDialect).Returns(new SqliteDialect());
        configuration.SetupGet(c => c.TablePrefix).Returns("tp_");
        configuration.SetupGet(c => c.Schema).Returns(string.Empty);
        var store = new Mock<IStore>();
        store.SetupGet(s => s.Configuration).Returns(configuration.Object);
        var session = new Mock<YesSql.ISession>();
        session.SetupGet(s => s.Store).Returns(store.Object);

        var connections = new Mock<IDbConnectionAccessor>(MockBehavior.Strict);

        var services = new ServiceCollection();

        if (withCaller)
        {
            services.AddSingleton<ICallerContextAccessor>(new CallerAccessor { Current = CallerContext.Anonymous("test", CallerSide.Site) });
        }

        if (scopes is not null)
        {
            var provider = new Mock<IScopeSetProvider>();
            provider.Setup(p => p.GetAsync(It.IsAny<CallerContext>(), It.IsAny<CancellationToken>())).ReturnsAsync(scopes);
            services.AddSingleton(provider.Object);
        }

        return new StructuredQuerySource(connections.Object, session.Object, new TestIndexCatalog(), services.BuildServiceProvider());
    }

    [Fact]
    public async Task Execute_NoneRuleReturnsNoRowsWithoutTouchingTheDatabase()
    {
        var source = Source(TestScopes.Of(("ContentItemIndex", ScopeRule.None)));

        var results = await source.ExecuteQueryAsync(SavedQuery(), new QueryRequest());

        Assert.Empty(results.Items);
        Assert.Equal(0, results.Total);
        Assert.Equal("ContentItemId", Assert.Single(results.Columns).Name);
    }

    [Fact]
    public async Task Execute_MissingScopeProviderRefuses()
    {
        var source = Source(scopes: null);

        await Assert.ThrowsAsync<ScopeRefusedException>(() => source.ExecuteQueryAsync(SavedQuery(), new QueryRequest()));
    }

    [Fact]
    public async Task Execute_NoCallerRefuses()
    {
        var source = Source(TestScopes.AllowAll, withCaller: false);

        await Assert.ThrowsAsync<ScopeRefusedException>(() => source.ExecuteQueryAsync(SavedQuery(), new QueryRequest()));
    }

    [Fact]
    public void Describe_ReportsParametersAndColumns()
    {
        var descriptor = Source(TestScopes.AllowAll).Describe(SavedQuery());

        Assert.NotNull(descriptor);
        Assert.Equal("posts", descriptor.Name);
        Assert.Equal(StructuredQuerySource.SourceName, descriptor.Source);
        Assert.Equal(typeof(string), Assert.Single(descriptor.Columns).Type);
    }

    [Fact]
    public void FromSchema_DescribesSavedSqlQueryColumns()
    {
        var query = new Query
        {
            Name = "q",
            Source = "Sql",
            Schema = new JsonObject { ["type"] = "object", ["properties"] = new JsonObject { ["Count"] = new JsonObject { ["type"] = "integer" }, ["Title"] = new JsonObject { ["type"] = "string" } } }.ToJsonString(),
        };

        var descriptor = QueryDescriptor.FromSchema(query);

        Assert.Equal([typeof(long), typeof(string)], descriptor.Columns.Select(column => column.Type));
    }

    private sealed class CallerAccessor : ICallerContextAccessor
    {
        public CallerContext? Current { get; set; }
    }
}
