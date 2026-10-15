#nullable enable
using Grove.Access;
using Grove.Data.Queries.Sql;
using Grove.Data.Queries.Structured;
using YesSql.Provider.Sqlite;
using YesSql.Provider.SqlServer;

namespace Grove.Tests.Queries;

/// <summary>What the SQL rewriter refuses: names that reach past the tenant, CTEs that shadow a table outside their WITH, functions off the allow-list, statements that write.</summary>
public class SqlParserRefusalTests
{
    private static readonly ISqlDialect s_sqlite = new SqliteDialect();

    // Only ContentItemIndex has a scope provider: any other real table is refused.
    private static ScopeSet ContentOnly() => TestScopes.Of(("ContentItemIndex", ScopeRule.All));

    private static bool TryParse(string sql, ScopeSet scopes, out string query, out IEnumerable<string> messages) =>
        SqlParser.TryParse(sql, string.Empty, s_sqlite, "tp_", new Dictionary<string, object>(), scopes, out query, out messages);

    [Fact]
    public void Cte_NameResolvesInsideItsOwnWith()
    {
        Assert.True(TryParse("WITH Document AS (SELECT 1 x) SELECT * FROM Document", ContentOnly(), out var query, out var messages), string.Join(' ', messages));
        Assert.EndsWith("SELECT * FROM [Document];", query.ReplaceLineEndings(" "));
    }

    [Fact]
    public void Cte_NameDoesNotLeakOutOfANestedWith()
    {
        // The outer Document is the real table: the WITH in the derived table does not name it.
        var exception = Assert.Throws<ScopeRefusedException>(() =>
            TryParse("SELECT d.* FROM Document d, (WITH Document AS (SELECT 1 x) SELECT x FROM Document) t", ContentOnly(), out _, out _));

        Assert.Equal("Document", exception.Target);
    }

    [Fact]
    public void Cte_BodyReadsTheRealTableOfItsOwnName()
    {
        // A non-recursive CTE is not in scope in its own body.
        var exception = Assert.Throws<ScopeRefusedException>(() =>
            TryParse("WITH Document AS (SELECT * FROM Document) SELECT * FROM Document", ContentOnly(), out _, out _));

        Assert.Equal("Document", exception.Target);
    }

    [Fact]
    public void Cte_BodyOfTheRealTableIsScoped()
    {
        var scopes = TestScopes.Of(("ContentItemIndex", ScopeRule.Where(ScopeFilter.Of(ScopeCondition.Equal("Owner", "u")))));

        Assert.True(TryParse("WITH ContentItemIndex AS (SELECT * FROM ContentItemIndex) SELECT * FROM ContentItemIndex", scopes, out var query, out var messages), string.Join(' ', messages));

        var sql = query.ReplaceLineEndings(" ");
        Assert.Contains("(SELECT * FROM [tp_ContentItemIndex] WHERE [tp_ContentItemIndex].[Owner] = @__scope0)", sql);
        Assert.EndsWith("SELECT * FROM [ContentItemIndex];", sql);
    }

    [Fact]
    public void Cte_RecursiveNameIsInScopeInItsBody()
    {
        Assert.True(TryParse("WITH RECURSIVE r(n) AS (SELECT 1 UNION ALL SELECT n + 1 FROM r WHERE n < 3) SELECT n FROM r", ContentOnly(), out _, out var messages), string.Join(' ', messages));
    }

    [Fact]
    public void Cte_LaterSiblingIsNotInScopeOfAnEarlierBody()
    {
        var exception = Assert.Throws<ScopeRefusedException>(() =>
            TryParse("WITH a AS (SELECT * FROM b), b AS (SELECT 1 x) SELECT * FROM a", ContentOnly(), out _, out _));

        Assert.Equal("b", exception.Target);
    }

    [Theory]
    [InlineData("SELECT * FROM otherschema.ContentItemIndex")]
    [InlineData("SELECT * FROM db.otherschema.ContentItemIndex")]
    [InlineData("SELECT c.DocumentId FROM ContentItemIndex c JOIN otherschema.ContentItemIndex o ON o.DocumentId = c.DocumentId")]
    public void MultiPartTableName_IsRefused(string sql)
    {
        Assert.False(TryParse(sql, ContentOnly(), out var query, out var messages));
        Assert.Null(query);
        Assert.Contains("Only a single-part table name is allowed.", messages);
    }

    [Theory]
    [InlineData("SELECT pg_read_file('/etc/passwd')")]
    [InlineData("SELECT load_extension('x')")]
    [InlineData("SELECT DocumentId FROM ContentItemIndex WHERE sqlite_version() > '3'")]
    [InlineData("SELECT DocumentId FROM ContentItemIndex ORDER BY xp_cmdshell('dir')")]
    public void ForbiddenFunction_IsRefused(string sql)
    {
        Assert.False(TryParse(sql, ContentOnly(), out var query, out var messages));
        Assert.Null(query);
        Assert.Contains(messages, message => message.Contains("is not allowed", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("SELECT COUNT(*), MAX(CreatedUtc), lower(DisplayText), coalesce(Author, Owner) FROM ContentItemIndex GROUP BY DisplayText, Author, Owner")]
    [InlineData("SELECT ROW_NUMBER() OVER (ORDER BY CreatedUtc) AS n, LAG(DocumentId) OVER (ORDER BY CreatedUtc) AS p FROM ContentItemIndex")]
    [InlineData("SELECT substr(DisplayText, 1, 3), round(1.5), strftime('%Y', CreatedUtc) FROM ContentItemIndex")]
    [InlineData("SELECT DocumentId FROM ContentItemIndex ORDER BY random()")]
    public void AllowedFunction_Passes(string sql)
    {
        Assert.True(TryParse(sql, ContentOnly(), out _, out var messages), string.Join(' ', messages));
    }

    [Theory]
    [InlineData("SELECT * INTO NewTable FROM ContentItemIndex")]
    [InlineData("SELECT DocumentId INTO #copy FROM ContentItemIndex")]
    public void SelectInto_IsRefused(string sql)
    {
        Assert.False(TryParse(sql, TestScopes.AllowAll, out var query, out var messages));
        Assert.Null(query);
        Assert.NotEmpty(messages);
        Assert.NotEmpty(SqlParser.Validate(sql));
    }

    [Fact]
    public void Page_Sqlite_WrapsTheStatementAndHoistsItsWith()
    {
        var parameters = new Dictionary<string, object>();

        Assert.True(SqlParser.TryParsePage(
            "WITH c AS (SELECT DocumentId FROM ContentItemIndex) SELECT DocumentId FROM c ORDER BY DocumentId;",
            string.Empty,
            s_sqlite,
            "tp_",
            parameters,
            ContentOnly(),
            out var page,
            out var count,
            out var messages), string.Join(' ', messages));

        Assert.Equal(
            "WITH c AS (SELECT [DocumentId] FROM [tp_ContentItemIndex]) SELECT * FROM (SELECT [DocumentId] FROM [c] ORDER BY [DocumentId]) AS q LIMIT @__take OFFSET @__skip;",
            page.ReplaceLineEndings(" "));
        Assert.Equal(
            "WITH c AS (SELECT [DocumentId] FROM [tp_ContentItemIndex]) SELECT COUNT(*) FROM (SELECT [DocumentId] FROM [c] ORDER BY [DocumentId]) AS q;",
            count.ReplaceLineEndings(" "));
    }

    [Fact]
    public void Page_SqlServer_OrdersByNothingAndKeepsTheInnerOrderLegal()
    {
        Assert.True(SqlParser.TryParsePage(
            "SELECT DocumentId FROM ContentItemIndex ORDER BY CreatedUtc DESC",
            string.Empty,
            new SqlServerDialect(),
            "tp_",
            new Dictionary<string, object>(),
            ContentOnly(),
            out var page,
            out var count,
            out var messages), string.Join(' ', messages));

        // SQL Server allows an ORDER BY in a derived table only with OFFSET; the outer order is (SELECT NULL).
        Assert.Equal(
            "SELECT * FROM (SELECT [DocumentId] FROM [tp_ContentItemIndex] ORDER BY [CreatedUtc] DESC OFFSET 0 ROWS) AS q ORDER BY (SELECT null) OFFSET @__skip ROWS FETCH NEXT @__take ROWS ONLY;",
            page.ReplaceLineEndings(" "),
            ignoreCase: true);
        Assert.StartsWith("SELECT COUNT(*) FROM (SELECT [DocumentId] FROM [tp_ContentItemIndex]", count.ReplaceLineEndings(" "));
    }

    [Fact]
    public void Page_RefusesMoreThanOneStatement()
    {
        Assert.False(SqlParser.TryParsePage(
            "SELECT DocumentId FROM ContentItemIndex; SELECT ContentItemId FROM ContentItemIndex",
            string.Empty,
            s_sqlite,
            "tp_",
            new Dictionary<string, object>(),
            ContentOnly(),
            out _,
            out _,
            out var messages));

        Assert.Contains("Only one statement is allowed.", messages);
    }

    [Theory]
    [InlineData(null, 100)]
    [InlineData(0, 100)]
    [InlineData(20, 20)]
    [InlineData(500, 500)]
    [InlineData(5000, 500)]
    public void PageSize_DefaultsAndIsCapped(int? requested, int expected)
    {
        Assert.Equal(expected, StructuredQueryCompiler.PageSize(requested));
    }
}
