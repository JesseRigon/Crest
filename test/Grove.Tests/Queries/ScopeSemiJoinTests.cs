#nullable enable
using System.Text.Json.Nodes;
using Grove.Access;
using Grove.ContentManagement.Records;
using Grove.Data.Scoping;
using Grove.Queries;
using Grove.Queries.Sql;
using Grove.Queries.Structured;
using Grove.Sharing.Indexes;
using YesSql.Provider.Sqlite;
using YesSql.Provider.SqlServer;

namespace Grove.Tests.Queries;

/// <summary>
/// The scope model's semi-joins (a column in a set selected from another index table) and time
/// comparisons, compiled by each of the three compilers: the structured compiler and the SQL
/// rewriter (as SQL text, SQLite and SQL Server) and the YesSql expression compiler (as an
/// expression YesSql translates). In memory a semi-join is refused.
/// </summary>
public class ScopeSemiJoinTests
{
    private static readonly DateTime s_now = new(2026, 10, 11, 12, 0, 0, DateTimeKind.Utc);

    /// <summary>ContentItemId IN (SELECT ObjectId FROM ShareAccessIndex WHERE Kind = 'content'
    /// AND (EndsUtc IS NULL OR EndsUtc &gt; now) AND Level &gt;= 1 AND DepthKey NOT IN (SELECT
    /// DepthKey FROM ShareRoleShadowIndex WHERE Role = 'customer')).</summary>
    private static ScopeRule Shared() => ScopeRule.Where(ScopeFilter.Of(ScopeCondition.InSelect(
        "ContentItemId",
        nameof(ShareAccessIndex),
        nameof(ShareAccessIndex.ObjectId),
        ScopeFilter.AllOf(
            ScopeFilter.Of(ScopeCondition.Equal(nameof(ShareAccessIndex.Kind), "content")),
            ScopeFilter.AnyOf(
                ScopeFilter.Of(ScopeCondition.IsNull(nameof(ShareAccessIndex.EndsUtc))),
                ScopeFilter.Of(ScopeCondition.GreaterThan(nameof(ShareAccessIndex.EndsUtc), s_now))),
            ScopeFilter.Of(ScopeCondition.GreaterThanOrEqual(nameof(ShareAccessIndex.Level), 1)),
            ScopeFilter.Of(ScopeCondition.NotInSelect(
                nameof(ShareAccessIndex.DepthKey),
                nameof(ShareRoleShadowIndex),
                nameof(ShareRoleShadowIndex.DepthKey),
                ScopeFilter.Of(ScopeCondition.Equal(nameof(ShareRoleShadowIndex.Role), "customer"))))))));

    private static StructuredQuery Items() => new()
    {
        Steps =
        [
            new FromStep { Source = "ContentItemIndex", Alias = "t0" },
            new FilterStep { Column = "ContentType", Operator = FilterOperator.Equals, Value = JsonValue.Create("BlogPost") },
            new ProjectStep { Columns = [new ProjectedColumn("ContentItemId")] },
        ],
    };

    private static CompiledStatement Compile(ISqlDialect dialect, ScopeRule rule) =>
        StructuredQueryCompiler.Compile(QueryPlan.Build(Items(), new TestIndexCatalog()), dialect, "tp_", string.Empty, TestScopes.Of(("ContentItemIndex", rule)), null, null, null);

    [Fact]
    public void Structured_CompilesTheSemiJoinWithItsOwnAliasPrefixAndParameters_ForSqlite()
    {
        var statement = Compile(new SqliteDialect(), Shared());

        Assert.Contains("t0.[ContentItemId] IN (SELECT __scope_select0.[ObjectId] FROM [tp_ShareAccessIndex] AS __scope_select0 WHERE (", statement.Sql);
        Assert.Contains("__scope_select0.[Kind] = @p0", statement.Sql);
        Assert.Contains("(__scope_select0.[EndsUtc] IS NULL OR __scope_select0.[EndsUtc] > @p1)", statement.Sql);
        Assert.Contains("__scope_select0.[Level] >= @p2", statement.Sql);
        Assert.Contains("__scope_select0.[DepthKey] NOT IN (SELECT __scope_select1.[DepthKey] FROM [tp_ShareRoleShadowIndex] AS __scope_select1 WHERE __scope_select1.[Role] = @p3)", statement.Sql);
        Assert.Equal("content", statement.Parameters["p0"]);
        Assert.Equal(s_now, statement.Parameters["p1"]);
        Assert.Equal(1, statement.Parameters["p2"]);
        Assert.Equal("customer", statement.Parameters["p3"]);
        Assert.DoesNotContain("customer", statement.Sql);

        // The count is scoped the same way.
        Assert.Contains("IN (SELECT __scope_select0.[ObjectId] FROM [tp_ShareAccessIndex]", statement.CountSql);
    }

    [Fact]
    public void Structured_CompilesTheSemiJoin_ForSqlServer()
    {
        var statement = Compile(new SqlServerDialect(), Shared());

        // SQL Server quotes aliases too.
        Assert.Contains("[t0].[ContentItemId] IN (SELECT [__scope_select0].[ObjectId] FROM [tp_ShareAccessIndex] AS [__scope_select0] WHERE (", statement.Sql);
        Assert.Contains("NOT IN (SELECT [__scope_select1].[DepthKey] FROM [tp_ShareRoleShadowIndex] AS [__scope_select1]", statement.Sql);
        Assert.Contains("[__scope_select0].[EndsUtc] > @p1", statement.Sql);
    }

    [Theory]
    [InlineData(ScopeOperator.LessThan, "<")]
    [InlineData(ScopeOperator.LessThanOrEqual, "<=")]
    [InlineData(ScopeOperator.GreaterThan, ">")]
    [InlineData(ScopeOperator.GreaterThanOrEqual, ">=")]
    public void Structured_CompilesComparisonsAndBindsInstantsAsUtc(ScopeOperator op, string sql)
    {
        var rule = ScopeRule.Where(ScopeFilter.Of(new ScopeCondition("PublishedUtc", op, [new DateTimeOffset(s_now)])));

        var statement = Compile(new SqliteDialect(), rule);

        Assert.Contains($"t0.[PublishedUtc] {sql} @p0", statement.Sql);
        Assert.Equal(s_now, statement.Parameters["p0"]);
    }

    [Fact]
    public void Structured_CompilesIsNullAndAComparisonWithNullAdmitsNothing()
    {
        var rule = ScopeRule.Where(ScopeFilter.AnyOf(
            ScopeFilter.Of(ScopeCondition.IsNull("PublishedUtc")),
            ScopeFilter.Of(new ScopeCondition("CreatedUtc", ScopeOperator.LessThan, [null]))));

        var statement = Compile(new SqliteDialect(), rule);

        Assert.Contains("(t0.[PublishedUtc] IS NULL OR 1 = 0)", statement.Sql);
    }

    private static string Rewrite(ISqlDialect dialect, ScopeRule rule, Dictionary<string, object> parameters)
    {
        var parsed = SqlParser.TryParse(
            "select ContentItemId from ContentItemIndex where ContentType = 'BlogPost'",
            string.Empty,
            dialect,
            "tp_",
            parameters,
            TestScopes.Of(("ContentItemIndex", rule)),
            out var query,
            out var messages);

        Assert.True(parsed, string.Join(' ', messages));
        return query.ReplaceLineEndings(" ");
    }

    [Fact]
    public void Rewriter_CompilesTheSemiJoinWithPrefixAliasAndBoundValues_ForSqlite()
    {
        var parameters = new Dictionary<string, object>();

        var sql = Rewrite(new SqliteDialect(), Shared(), parameters);

        Assert.Contains("[tp_ContentItemIndex].[ContentItemId] IN (SELECT __scope_select0.[ObjectId] FROM [tp_ShareAccessIndex]", sql);
        Assert.Contains("__scope_select0.[Kind] = @__scope0", sql);
        Assert.Contains("__scope_select0.[EndsUtc] IS NULL OR __scope_select0.[EndsUtc] > @__scope1", sql);
        Assert.Contains("__scope_select0.[Level] >= @__scope2", sql);
        Assert.Contains("__scope_select0.[DepthKey] NOT IN (SELECT __scope_select1.[DepthKey] FROM [tp_ShareRoleShadowIndex]", sql);
        Assert.Contains("__scope_select1.[Role] = @__scope3", sql);
        Assert.Equal("content", parameters["__scope0"]);
        Assert.Equal(s_now, parameters["__scope1"]);
        Assert.Equal("customer", parameters["__scope3"]);
        Assert.DoesNotContain("customer", sql);
    }

    [Fact]
    public void Rewriter_CompilesTheSemiJoin_ForSqlServer()
    {
        var parameters = new Dictionary<string, object>();

        var sql = Rewrite(new SqlServerDialect(), Shared(), parameters);

        Assert.Contains("[tp_ContentItemIndex].[ContentItemId] IN (SELECT __scope_select0.[ObjectId] FROM [tp_ShareAccessIndex]", sql);
        Assert.Contains("NOT IN (SELECT __scope_select1.[DepthKey] FROM [tp_ShareRoleShadowIndex]", sql);
    }

    [Fact]
    public void Rewriter_CompilesComparisonsAndIsNull()
    {
        var parameters = new Dictionary<string, object>();
        var rule = ScopeRule.Where(ScopeFilter.AllOf(
            ScopeFilter.Of(ScopeCondition.LessThanOrEqual("CreatedUtc", new DateTimeOffset(s_now))),
            ScopeFilter.Of(ScopeCondition.IsNull("PublishedUtc"))));

        var sql = Rewrite(new SqliteDialect(), rule, parameters);

        Assert.Contains("[tp_ContentItemIndex].[CreatedUtc] <= @__scope0", sql);
        Assert.Contains("[tp_ContentItemIndex].[PublishedUtc] IS NULL", sql);
        Assert.Equal(s_now, parameters["__scope0"]);
    }

    [Fact]
    public void Rewriter_DoesNotRequireAScopeForTheSelectedTable()
    {
        // ShareAccessIndex has no scope provider in the set: the semi-join is part of the
        // scope, not a table the template reads.
        var sql = Rewrite(new SqliteDialect(), Shared(), []);

        Assert.Contains("[tp_ShareAccessIndex]", sql);
    }

    [Fact]
    public void Expressions_CompileTheSemiJoinToYesSqlsSubquery()
    {
        var text = ScopeExpressions.ToPredicate<ContentItemIndex>(Shared()).ToString();

        Assert.Contains("Convert(index.ContentItemId, Object).IsIn(inner => Convert(inner.ObjectId, Object)", text);
        Assert.Contains("inner.Kind == \"content\"", text);
        Assert.Contains("inner.EndsUtc > ", text);
        Assert.Contains("inner.Level >= 1", text);
        Assert.Contains("Convert(inner.DepthKey, Object).IsNotIn(inner => Convert(inner.DepthKey, Object)", text);
    }

    [Fact]
    public void Expressions_CompileComparisonsAndIsNull()
    {
        var rule = ScopeRule.Where(ScopeFilter.AllOf(
            ScopeFilter.Of(ScopeCondition.GreaterThan("CreatedUtc", new DateTimeOffset(s_now))),
            ScopeFilter.Of(ScopeCondition.IsNull("PublishedUtc"))));

        var predicate = ScopeExpressions.ToPredicate<ContentItemIndex>(rule);
        var text = predicate.ToString();

        Assert.Contains("index.CreatedUtc > ", text);
        Assert.Contains("index.PublishedUtc == null", text);

        var compiled = predicate.Compile();
        Assert.True(compiled(new() { CreatedUtc = s_now.AddMinutes(1) }));
        Assert.False(compiled(new() { CreatedUtc = s_now }));
        Assert.False(compiled(new() { CreatedUtc = s_now.AddMinutes(1), PublishedUtc = s_now }));
    }

    [Fact]
    public void InMemory_ASemiJoinIsRefused()
    {
        var filter = Shared().Filter!;

        Assert.True(filter.HasSelect);
        Assert.Throws<NotSupportedException>(() => filter.Matches(_ => "x"));

        // Even where it would not be reached.
        var either = ScopeFilter.AnyOf(ScopeFilter.Of(ScopeCondition.Equal("A", "x")), filter);
        Assert.Throws<NotSupportedException>(() => either.Matches(_ => "x"));
    }

    [Fact]
    public void InMemory_ComparisonsAndIsNull()
    {
        var active = ScopeFilter.AllOf(
            ScopeFilter.AnyOf(ScopeFilter.Of(ScopeCondition.IsNull("Starts")), ScopeFilter.Of(ScopeCondition.LessThanOrEqual("Starts", s_now))),
            ScopeFilter.AnyOf(ScopeFilter.Of(ScopeCondition.IsNull("Ends")), ScopeFilter.Of(ScopeCondition.GreaterThan("Ends", new DateTimeOffset(s_now)))));

        Assert.False(active.HasSelect);
        Assert.True(active.Matches(_ => null));
        Assert.True(active.Matches(column => column == "Ends" ? s_now.AddSeconds(1) : null));
        Assert.False(active.Matches(column => column == "Ends" ? s_now : null));
        Assert.False(active.Matches(column => column == "Starts" ? s_now.AddSeconds(1) : null));
        Assert.True(ScopeFilter.Of(ScopeCondition.GreaterThanOrEqual("Level", 1)).Matches(_ => 3L));
        Assert.False(ScopeFilter.Of(ScopeCondition.LessThan("Level", 1)).Matches(_ => null));
    }
}
