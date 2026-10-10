#nullable enable
using Crest.Access;
using Crest.Queries.Sql;
using YesSql.Provider.Sqlite;

namespace Crest.Tests.Queries;

public class SqlParserScopeTests
{
    private static readonly ISqlDialect s_sqlite = new SqliteDialect();

    private static string Parse(string sql, ScopeSet scopes, Dictionary<string, object> parameters)
    {
        var parsed = SqlParser.TryParse(sql, string.Empty, s_sqlite, "tp_", parameters, scopes, out var query, out var messages);

        Assert.True(parsed, string.Join(' ', messages));

        return query.ReplaceLineEndings(" ");
    }

    [Fact]
    public void Parse_ConjoinsScopePredicateAsParameters()
    {
        var scopes = TestScopes.Of(("ContentItemIndex", ScopeRule.Where(ScopeFilter.Of(ScopeCondition.Equal("Owner", "user-1")))));
        var parameters = new Dictionary<string, object> { ["type"] = "BlogPost" };

        var sql = Parse("select ContentItemId from ContentItemIndex where ContentType = @type", scopes, parameters);

        Assert.Contains("WHERE ([ContentType] = @type) AND [tp_ContentItemIndex].[Owner] = @__scope0", sql);
        Assert.Equal("user-1", parameters["__scope0"]);
        Assert.DoesNotContain("user-1", sql);
    }

    [Fact]
    public void Parse_ScopesAliasedTablesSubqueriesAndSetOperationBranches()
    {
        var scopes = TestScopes.Of(
            ("ContentItemIndex", ScopeRule.Where(ScopeFilter.Of(ScopeCondition.In("Owner", ["a", "b"])))),
            ("PathPartIndex", ScopeRule.None));
        var parameters = new Dictionary<string, object>();

        var sql = Parse(
            "select ci.ContentItemId from ContentItemIndex ci where ci.DocumentId in (select DocumentId from PathPartIndex) union select ContentItemId from ContentItemIndex",
            scopes,
            parameters);

        Assert.Contains("ci.[Owner] IN (@__scope0, @__scope1)", sql);
        Assert.Contains("FROM [tp_PathPartIndex] WHERE 1 = 0", sql);
        Assert.Contains("FROM [tp_ContentItemIndex] WHERE [tp_ContentItemIndex].[Owner] IN (@__scope2, @__scope3)", sql);
        Assert.Equal(new object[] { "a", "b", "a", "b" }, new[] { "__scope0", "__scope1", "__scope2", "__scope3" }.Select(name => parameters[name]).ToArray());
    }

    [Fact]
    public void Parse_ScopesCteBodyNotCteName()
    {
        var scopes = TestScopes.Of(("ContentItemIndex", ScopeRule.Where(ScopeFilter.Of(ScopeCondition.Equal("Owner", "u")))));

        var sql = Parse("with recent as (select DocumentId from ContentItemIndex) select DocumentId from recent", scopes, []);

        Assert.Contains("(SELECT [DocumentId] FROM [tp_ContentItemIndex] WHERE [tp_ContentItemIndex].[Owner] = @__scope0)", sql);
        Assert.EndsWith("SELECT [DocumentId] FROM [recent];", sql);
    }

    [Fact]
    public void Parse_LeftJoinScopeGoesIntoJoinCondition()
    {
        var scopes = TestScopes.Of(
            ("ContentItemIndex", ScopeRule.All),
            ("PathPartIndex", ScopeRule.Where(ScopeFilter.Of(ScopeCondition.Equal("Path", "/p")))));

        var sql = Parse("select c.ContentItemId from ContentItemIndex c left join PathPartIndex p on p.DocumentId = c.DocumentId", scopes, []);

        Assert.Contains("ON (p.[DocumentId] = c.[DocumentId]) AND p.[Path] = @__scope0", sql);
        Assert.DoesNotContain("WHERE", sql);
    }

    [Fact]
    public void Parse_RefusesTableWithoutScopeProvider()
    {
        var scopes = TestScopes.Of(("ContentItemIndex", ScopeRule.All));

        var exception = Assert.Throws<ScopeRefusedException>(() =>
            SqlParser.TryParse("select * from Document", string.Empty, s_sqlite, "tp_", new Dictionary<string, object>(), scopes, out _, out _));

        Assert.Equal("Document", exception.Target);
    }

    [Theory]
    [InlineData("select * from ContentItemIndex where ContentType = '{{ type }}'")]
    [InlineData("{% if x %}select * from ContentItemIndex{% endif %}")]
    public void Validate_RejectsLiquid(string sql)
    {
        Assert.Contains(SqlParser.LiquidNotAllowedMessage, SqlParser.Validate(sql));

        var parsed = SqlParser.TryParse(sql, string.Empty, s_sqlite, "tp_", new Dictionary<string, object>(), TestScopes.AllowAll, out var query, out var messages);

        Assert.False(parsed);
        Assert.Null(query);
        Assert.Contains(SqlParser.LiquidNotAllowedMessage, messages);
    }

    [Fact]
    public void Parse_ParametersNeverAppearInSql()
    {
        var parameters = new Dictionary<string, object> { ["type"] = "BlogPost' OR 1=1 --" };

        var sql = Parse("select ContentItemId from ContentItemIndex where ContentType = @type and Owner = @owner:'default-owner'", TestScopes.AllowAll, parameters);

        Assert.DoesNotContain("BlogPost", sql);
        Assert.DoesNotContain("default-owner", sql);
        Assert.Contains("@type", sql);
        Assert.Contains("@owner", sql);
        Assert.Equal("default-owner", parameters["owner"]);
    }
}
