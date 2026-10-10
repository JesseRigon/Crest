#nullable enable
using System.Text.Json;
using System.Text.Json.Nodes;
using Crest.Access;
using Crest.Queries;
using Crest.Queries.Structured;
using YesSql.Provider.Sqlite;

namespace Crest.Tests.Queries;

public class StructuredQueryCompilerTests
{
    private static readonly ISqlDialect s_sqlite = new SqliteDialect();
    private static readonly TestIndexCatalog s_catalog = new();

    private static StructuredQuery BlogPosts() => new()
    {
        Parameters = [new QueryParameter("owner", QueryParameterType.String, Required: true)],
        Steps =
        [
            new FromStep { Source = "ContentItemIndex", Alias = "t0" },
            new FilterStep { Column = "ContentType", Operator = FilterOperator.Equals, Value = JsonValue.Create("BlogPost") },
            new FilterStep { Column = "Owner", Operator = FilterOperator.Equals, Parameter = "owner" },
            new ProjectStep { Columns = [new ProjectedColumn("ContentItemId"), new ProjectedColumn("DisplayText", "Title"), new ProjectedColumn("Published")] },
            new SortStep { Columns = [new SortColumn("CreatedUtc", Descending: true)] },
            new PageStep { Size = 10 },
        ],
    };

    private static CompiledStatement Compile(StructuredQuery query, ScopeSet scopes, IDictionary<string, object>? parameters = null, string? pageToken = null) =>
        StructuredQueryCompiler.Compile(QueryPlan.Build(query, s_catalog), s_sqlite, "tp_", string.Empty, scopes, parameters, pageToken, null);

    [Fact]
    public void Compile_EmitsParameterisedSqlWithScopeConjoined_ForSqlite()
    {
        var scopes = TestScopes.Of(("ContentItemIndex", ScopeRule.Where(ScopeFilter.Of(ScopeCondition.Equal("Owner", "user-1")))));

        var statement = Compile(BlogPosts(), scopes, new Dictionary<string, object> { ["owner"] = "user-1" });

        Assert.False(statement.IsEmpty);
        Assert.StartsWith("SELECT t0.[ContentItemId] AS ContentItemId, t0.[DisplayText] AS Title, t0.[Published] AS Published FROM [tp_ContentItemIndex] AS t0 WHERE", statement.Sql);
        Assert.Contains("t0.[Owner] = @p0", statement.Sql);
        Assert.Contains("t0.[ContentType] = @p1", statement.Sql);
        Assert.Contains("t0.[Owner] = @p2", statement.Sql);
        Assert.EndsWith("ORDER BY t0.[CreatedUtc] DESC, t0.[DocumentId] LIMIT @__take OFFSET @__skip", statement.Sql);
        Assert.Equal("user-1", statement.Parameters["p0"]);
        Assert.Equal("BlogPost", statement.Parameters["p1"]);
        Assert.Equal("user-1", statement.Parameters["p2"]);
        Assert.Equal(0, statement.Parameters["__skip"]);
        Assert.Equal(10, statement.Parameters["__take"]);
        Assert.Equal("SELECT COUNT(*) FROM [tp_ContentItemIndex] AS t0 WHERE t0.[Owner] = @p0 AND t0.[ContentType] = @p1 AND t0.[Owner] = @p2", statement.CountSql);
    }

    [Fact]
    public void Compile_NeverSplicesValuesIntoSql()
    {
        var scopes = TestScopes.Of(("ContentItemIndex", ScopeRule.Where(ScopeFilter.Of(ScopeCondition.In("Owner", ["scope-secret", "other"])))));

        var statement = Compile(BlogPosts(), scopes, new Dictionary<string, object> { ["owner"] = "bound-secret' OR 1=1 --" }, PageTokens.Encode(30));

        Assert.DoesNotContain("secret", statement.Sql);
        Assert.DoesNotContain("BlogPost", statement.Sql);
        Assert.DoesNotContain("30", statement.Sql);
        Assert.Contains("t0.[Owner] IN (@p0, @p1)", statement.Sql);
        Assert.Equal("bound-secret' OR 1=1 --", statement.Parameters["p3"]);
        Assert.Equal(30, statement.Parameters["__skip"]);
    }

    [Fact]
    public void Compile_RefusesTableWithoutScopeProvider()
    {
        var scopes = TestScopes.Of(("SomethingElse", ScopeRule.All));

        var exception = Assert.Throws<ScopeRefusedException>(() => Compile(BlogPosts(), scopes, new Dictionary<string, object> { ["owner"] = "u" }));

        Assert.Equal("ContentItemIndex", exception.Target);
    }

    [Fact]
    public void Compile_NoneRuleProducesEmptyStatement()
    {
        var statement = Compile(BlogPosts(), TestScopes.Of(("ContentItemIndex", ScopeRule.None)), new Dictionary<string, object> { ["owner"] = "u" });

        Assert.True(statement.IsEmpty);
    }

    [Fact]
    public void Compile_RequiredParameterMissing_Throws()
    {
        var exception = Assert.Throws<StructuredQueryException>(() => Compile(BlogPosts(), TestScopes.AllowAll));

        Assert.Contains("'owner' is required", exception.Message);
    }

    [Theory]
    [InlineData(ScopeOperator.In, "1 = 0")]
    [InlineData(ScopeOperator.NotIn, "1 = 1")]
    public void Compile_EmptyInAndNotIn(ScopeOperator op, string expected)
    {
        var scopes = TestScopes.Of(("ContentItemIndex", ScopeRule.Where(ScopeFilter.Of(new ScopeCondition("ContentType", op, [])))));

        var statement = Compile(BlogPosts(), scopes, new Dictionary<string, object> { ["owner"] = "u" });

        Assert.Contains("WHERE " + expected + " AND", statement.Sql);
    }

    [Fact]
    public void Compile_NotInWithValues_RendersNotIn()
    {
        var scopes = TestScopes.Of(("ContentItemIndex", ScopeRule.Where(ScopeFilter.Of(ScopeCondition.NotIn("ContentType", ["Secret", "Hidden"])))));

        var statement = Compile(BlogPosts(), scopes, new Dictionary<string, object> { ["owner"] = "u" });

        Assert.Contains("t0.[ContentType] NOT IN (@p0, @p1)", statement.Sql);
        Assert.Equal("Secret", statement.Parameters["p0"]);
    }

    [Fact]
    public void Compile_LeftJoinScopeKeepsUnmatchedRows()
    {
        var query = new StructuredQuery
        {
            Steps =
            [
                new FromStep { Source = "ContentItemIndex", Alias = "c" },
                new JoinStep { Index = "PathPartIndex", Alias = "p", Kind = JoinKind.Left, Column = "DocumentId", ToColumn = "c.DocumentId" },
                new ProjectStep { Columns = [new ProjectedColumn("c.ContentItemId"), new ProjectedColumn("p.Path")] },
            ],
        };

        var scopes = TestScopes.Of(
            ("ContentItemIndex", ScopeRule.All),
            ("PathPartIndex", ScopeRule.Where(ScopeFilter.AnyOf(ScopeFilter.Of(ScopeCondition.Equal("Path", "/a")), ScopeFilter.Of(ScopeCondition.Equal("Path", null))))));

        var statement = Compile(query, scopes);

        Assert.Contains("LEFT JOIN [tp_PathPartIndex] AS p ON [c].[DocumentId] = p.[DocumentId]", statement.Sql);
        Assert.Contains("WHERE ((p.[Path] = @p0 OR p.[Path] IS NULL) OR p.[DocumentId] IS NULL)", statement.Sql);
        Assert.EndsWith("ORDER BY c.[DocumentId] LIMIT @__take OFFSET @__skip", statement.Sql);
        Assert.Equal(StructuredQueryCompiler.DefaultPageSize, statement.PageSize);
        Assert.NotNull(statement.CountSql);
    }

    [Fact]
    public void Compile_ContentTypeSource_ReadsPublishedItemsOfTheType()
    {
        var query = new StructuredQuery
        {
            Steps = [new FromStep { Source = "Article" }, new ProjectStep { Columns = [new ProjectedColumn("DocumentId")] }],
        };

        var scopes = TestScopes.Of(("ContentItemIndex", ScopeRule.All), ("Article", ScopeRule.Where(ScopeFilter.Of(ScopeCondition.Equal("Author", "me")))));

        var statement = Compile(query, scopes);

        Assert.Contains("FROM [tp_ContentItemIndex] AS Article WHERE Article.[Author] = @p0 AND Article.[ContentType] = @p1 AND Article.[Published] = @p2", statement.Sql);
        Assert.Equal("Article", statement.Parameters["p1"]);
        Assert.Equal(true, statement.Parameters["p2"]);
        Assert.Equal("DocumentId", statement.DocumentIdColumn);
    }

    [Theory]
    [InlineData(null, 10)]
    [InlineData(5, 5)]
    [InlineData(50, 50)]
    [InlineData(9000, StructuredQueryCompiler.MaxPageSize)]
    public void Compile_RequestPageSizeOverridesTheQuerysAndIsCapped(int? requested, int expected)
    {
        var statement = StructuredQueryCompiler.Compile(QueryPlan.Build(BlogPosts(), s_catalog), s_sqlite, "tp_", string.Empty, TestScopes.AllowAll, new Dictionary<string, object> { ["owner"] = "u" }, null, requested);

        Assert.Equal(expected, statement.PageSize);
    }

    [Fact]
    public void Compile_UnpagedQueryUsesDefaultAndCap()
    {
        var query = new StructuredQuery { Steps = [new FromStep { Source = "ContentItemIndex" }] };
        var plan = QueryPlan.Build(query, s_catalog);

        Assert.Equal(100, StructuredQueryCompiler.Compile(plan, s_sqlite, "tp_", "", TestScopes.AllowAll, null, null, null).PageSize);
        Assert.Equal(500, StructuredQueryCompiler.Compile(plan, s_sqlite, "tp_", "", TestScopes.AllowAll, null, null, 9000).PageSize);
    }

    [Fact]
    public void Plan_DerivesOutputSchemaFromIndexTypes()
    {
        var plan = QueryPlan.Build(BlogPosts(), s_catalog);

        Assert.Equal(["ContentItemId", "Title", "Published"], plan.OutputColumns.Select(column => column.Name));
        Assert.Equal([typeof(string), typeof(string), typeof(bool)], plan.OutputColumns.Select(column => column.Type));
        Assert.Single(plan.Parameters);
    }

    [Fact]
    public void Plan_RejectsUnknownColumn()
    {
        var query = new StructuredQuery { Steps = [new FromStep { Source = "ContentItemIndex" }, new FilterStep { Column = "Nope", Operator = FilterOperator.IsNull }] };

        Assert.Throws<StructuredQueryException>(() => QueryPlan.Build(query, s_catalog));
    }

    [Fact]
    public void Model_RoundTripsAsJson()
    {
        var json = JsonSerializer.Serialize(BlogPosts());
        var query = JsonSerializer.Deserialize<StructuredQuery>(json)!;

        Assert.Contains("\"step\":\"from\"", json);
        Assert.DoesNotContain("SELECT", json, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(6, query.Steps.Count);
        Assert.IsType<PageStep>(query.Steps[^1]);
        Assert.Equal(QueryParameterType.String, query.Parameters[0].Type);
    }
}
