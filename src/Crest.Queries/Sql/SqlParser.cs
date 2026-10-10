using Cyqwel.Ast;
using Cyqwel.Dialects;
using Cyqwel.Generation;
using Cyqwel.Validation;
using Cyqwel.Visitors;
using Crest.Access;
using YesSql;
using YesSql.Provider.MySql;
using YesSql.Provider.PostgreSql;
using YesSql.Provider.Sqlite;
using YesSql.Provider.SqlServer;

namespace Crest.Queries.Sql;

public class SqlParser
{
    private const string RandomFunctionName = "__platform_random__";

    private static readonly SqlGenerationOptions GenerationOptions = new()
    {
        FunctionNameCase = FunctionNameCase.Preserve,
    };

    private static readonly Cyqwel.Dialects.SqlDialect ParserDialect = SqlDialectBuilder
        .Create("orchard")
        .BasedOn(SqlDialects.Generic)
        .ConfigureParser(static options => options with { SupportsParameterDefaults = true })
        .Build();

    public const string LiquidNotAllowedMessage = "Liquid ('{{' or '{%') is not allowed in a SQL query; the template is parsed as written and values bind as parameters (@name).";

    public static bool ContainsLiquid(string sql) =>
        sql is not null
        && (sql.Contains("{{", StringComparison.Ordinal) || sql.Contains("{%", StringComparison.Ordinal));

    internal static IReadOnlyList<string> Validate(string sql)
    {
        if (ContainsLiquid(sql))
        {
            return [LiquidNotAllowedMessage];
        }

        var result = SqlValidator.Validate(
            sql,
            ParserDialect);

        if (!result.IsValid)
        {
            return result.Diagnostics
                .Where(static diagnostic => diagnostic.Severity == SqlValidationSeverity.Error)
                .Select(static diagnostic => diagnostic.Location is { } location
                    ? $"Parse error: {diagnostic.Message} at line {location.Line}, column {location.Column}"
                    : $"Parse error: {diagnostic.Message}")
                .ToArray();
        }

        if (!ParserDialect.TryParse(sql, out var document, out var error))
        {
            return error is null
                ? ["Parse error: Unknown parsing error"]
                : [$"Parse error: {error.Message} at line {error.Line}, column {error.Column}"];
        }

        return ContainsNonQueryStatement(document!)
            ? ["Only SELECT statements are supported."]
            : [];
    }

    public static bool TryParse(
        string sql,
        string schema,
        ISqlDialect dialect,
        string tablePrefix,
        IDictionary<string, object> parameters,
        ScopeSet scopes,
        out string query,
        out IEnumerable<string> messages)
    {
        ArgumentNullException.ThrowIfNull(scopes);

        if (ContainsLiquid(sql))
        {
            query = null;
            messages = [LiquidNotAllowedMessage];
            return false;
        }

        if (!ParserDialect.TryParse(sql, out var document, out var error))
        {
            query = null;
            messages = error is null
                ? ["Parse error: Unknown parsing error"]
                : [$"Parse error: {error.Message} at position {error.Offset}"];
            return false;
        }

        if (ContainsNonQueryStatement(document!))
        {
            query = null;
            messages = ["Only SELECT statements are supported."];
            return false;
        }

        try
        {
            var names = new SqlNameCollector();
            names.Visit(document);

            var rewriter = new PlatformSqlRewriter(
                schema,
                tablePrefix,
                dialect,
                parameters,
                scopes,
                names.TableAliases,
                names.CommonTableExpressions);

            var rewritten = rewriter.Visit(document);
            query = new PlatformSqlDialect(dialect).Generate(rewritten, GenerationOptions) + ";";
            messages = [];
            return true;
        }
        catch (ScopeRefusedException)
        {
            // A table with no scope rule is refused, never run unfiltered.
            throw;
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException or NotSupportedException)
        {
            query = null;
            messages = [exception.Message];
            return false;
        }
        catch (Exception exception)
        {
            query = null;
            messages = ["Unexpected error: " + exception.Message];
            return false;
        }
    }

    private static bool ContainsNonQueryStatement(SqlNode node) =>
        node.DescendantsAndSelf().OfType<SqlStatement>().Any(static statement => statement is not SqlQuery);

    private sealed class SqlNameCollector : SqlVisitor
    {
        public HashSet<string> TableAliases { get; } = [];

        public HashSet<string> CommonTableExpressions { get; } = [];

        protected override void VisitNamedTable(NamedTable node)
        {
            if (node.Alias is not null)
            {
                TableAliases.Add(node.Alias.Value);
            }

            base.VisitNamedTable(node);
        }

        protected override void VisitDerivedTable(DerivedTable node)
        {
            TableAliases.Add(node.Alias.Value);
            base.VisitDerivedTable(node);
        }

        protected override void VisitCommonTableExpression(CommonTableExpression node)
        {
            CommonTableExpressions.Add(node.Name.Value);
            base.VisitCommonTableExpression(node);
        }
    }

    private sealed class PlatformSqlRewriter(
        string schema,
        string tablePrefix,
        ISqlDialect dialect,
        IDictionary<string, object> parameters,
        ScopeSet scopes,
        HashSet<string> tableAliases,
        HashSet<string> commonTableExpressions) : SqlRewriter
    {
        private int _scopeParameters;
        private readonly IDictionary<string, object> _scopeValues = parameters ?? new Dictionary<string, object>();

        protected override SqlNode VisitSelect(SelectStatement node)
        {
            var rewritten = (SelectStatement)base.VisitSelect(node);

            // Scope is a step of every select: each table it reads from (the From tree of this
            // select; subqueries, CTE bodies and set-operation branches are selects of their own
            // and get their own step) is narrowed to what the caller may see.
            if (node.From is not null)
            {
                var predicates = new List<SqlExpression>();
                var from = ScopeTableSource(node.From, rewritten.From, predicates, JoinKind.Inner, isRightSide: false);
                var where = Conjoin(rewritten.Where, predicates);

                if (!ReferenceEquals(from, rewritten.From) || !ReferenceEquals(where, rewritten.Where))
                {
                    rewritten = rewritten with { From = from, Where = where };
                }
            }

            var projections = rewritten.Projections;

            for (var i = 0; i < projections.Count; i++)
            {
                if (projections[i].Expression is not LiteralExpression { Value: bool value })
                {
                    continue;
                }

                if (ReferenceEquals(projections, rewritten.Projections))
                {
                    projections = rewritten.Projections.ToArray();
                }

                ((SelectItem[])projections)[i] = projections[i] with
                {
                    Expression = new ColumnExpression([Quote(value.ToString().ToLowerInvariant())]),
                };
            }

            var limit = GetLimit(rewritten.Limit, rewritten.Offset);

            return ReferenceEquals(projections, rewritten.Projections) && ReferenceEquals(limit, rewritten.Limit)
                ? rewritten
                : rewritten with { Projections = projections, Limit = limit };
        }

        protected override SqlNode VisitSetOperation(SetOperationStatement node)
        {
            var rewritten = (SetOperationStatement)base.VisitSetOperation(node);
            var limit = GetLimit(rewritten.Limit, rewritten.Offset);

            return ReferenceEquals(limit, rewritten.Limit)
                ? rewritten
                : rewritten with { Limit = limit };
        }

        protected override SqlNode VisitTableName(TableName node)
        {
            if (node.Parts.Count == 0)
            {
                return node;
            }

            var parts = new List<SqlIdentifier>(node.Parts.Count + 1);
            var first = node.Parts[0];

            if (commonTableExpressions.Contains(first.Value))
            {
                parts.Add(Quote(first.Value));
            }
            else
            {
                parts.AddRange(GetTableNameParts(tablePrefix + first.Value));
            }

            for (var i = 1; i < node.Parts.Count; i++)
            {
                parts.Add(Quote(node.Parts[i].Value));
            }

            return node with { Parts = parts };
        }

        protected override SqlNode VisitColumn(ColumnExpression node)
        {
            if (node.Parts.Count == 0)
            {
                return node;
            }

            if (node.Parts.Count == 1)
            {
                return node with { Parts = [Quote(node.Parts[0].Value)] };
            }

            var parts = new List<SqlIdentifier>(node.Parts.Count + 1);
            var qualifier = node.Parts[0];

            if (tableAliases.Contains(qualifier.Value))
            {
                parts.Add(qualifier with { IsQuoted = false });
            }
            else
            {
                parts.AddRange(GetTableNameParts(tablePrefix + qualifier.Value));
            }

            for (var i = 1; i < node.Parts.Count; i++)
            {
                parts.Add(Quote(node.Parts[i].Value));
            }

            return node with { Parts = parts };
        }

        protected override SqlNode VisitParameter(ParameterExpression node)
        {
            if (parameters is not null && !parameters.ContainsKey(node.Name))
            {
                parameters[node.Name] = node.DefaultValue switch
                {
                    LiteralExpression { Value: bool or string or decimal or long } literal => literal.Value,
                    null => throw new InvalidOperationException($"Missing parameter: {node.Name}"),
                    _ => throw new InvalidOperationException("Unsupported default parameter value type"),
                };
            }

            return node;
        }

        protected override SqlNode VisitOrderByItem(OrderByItem node)
        {
            var rewritten = (OrderByItem)base.VisitOrderByItem(node);

            return rewritten.Expression is FunctionCallExpression
                {
                    Name.Value: var name,
                    Arguments.Count: 0,
                } function
                && name.Equals("random", StringComparison.OrdinalIgnoreCase)
                    ? rewritten with
                    {
                        Expression = function with { Name = new SqlIdentifier(RandomFunctionName) },
                    }
                    : rewritten;
        }

        /// <summary>
        /// Walks the original and the rewritten From trees together (same shape) and returns the
        /// rewritten tree with the scope predicate of every named table applied: into the join
        /// condition for the outer side of a one-sided outer join, into the WHERE list otherwise.
        /// </summary>
        private TableSource ScopeTableSource(TableSource original, TableSource rewritten, List<SqlExpression> predicates, JoinKind parentKind, bool isRightSide)
        {
            switch (original)
            {
                case NamedTable table when rewritten is NamedTable:
                    var predicate = ScopePredicate(table);

                    if (predicate is null)
                    {
                        return rewritten;
                    }

                    predicates.Add(predicate);
                    return rewritten;

                case JoinTable join when rewritten is JoinTable rewrittenJoin:
                    var leftPredicates = new List<SqlExpression>();
                    var rightPredicates = new List<SqlExpression>();
                    var left = ScopeTableSource(join.Left, rewrittenJoin.Left, leftPredicates, join.Kind, isRightSide: false);
                    var right = ScopeTableSource(join.Right, rewrittenJoin.Right, rightPredicates, join.Kind, isRightSide: true);
                    var condition = rewrittenJoin.Condition;

                    // The outer side of a one-sided outer join keeps its unmatched rows: its
                    // predicate belongs to ON, so an out-of-scope row becomes a null row instead
                    // of dropping the row it is joined to.
                    if (join.Kind == JoinKind.Left && join.Right is NamedTable && condition is not null && rightPredicates.Count > 0)
                    {
                        condition = Conjoin(condition, rightPredicates);
                        rightPredicates.Clear();
                    }
                    else if (join.Kind == JoinKind.Right && join.Left is NamedTable && condition is not null && leftPredicates.Count > 0)
                    {
                        condition = Conjoin(condition, leftPredicates);
                        leftPredicates.Clear();
                    }

                    predicates.AddRange(leftPredicates);
                    predicates.AddRange(rightPredicates);

                    return ReferenceEquals(left, rewrittenJoin.Left) && ReferenceEquals(right, rewrittenJoin.Right) && ReferenceEquals(condition, rewrittenJoin.Condition)
                        ? rewrittenJoin
                        : rewrittenJoin with { Left = left, Right = right, Condition = condition };

                default:
                    // A derived table is a select of its own and was scoped when visited.
                    return rewritten;
            }
        }

        private SqlExpression ScopePredicate(NamedTable table)
        {
            if (table.Name.Parts.Count == 0)
            {
                return null;
            }

            var name = table.Name.Parts[^1].Value;

            if (table.Name.Parts.Count == 1 && commonTableExpressions.Contains(name))
            {
                return null;
            }

            var rule = scopes.Require(name);

            if (rule.Kind == ScopeKind.All)
            {
                return null;
            }

            IReadOnlyList<SqlIdentifier> qualifier = table.Alias is not null
                ? [table.Alias with { IsQuoted = false }]
                : GetTableNameParts(tablePrefix + name);

            return rule.Kind == ScopeKind.None
                ? False()
                : Render(rule.Filter, qualifier);
        }

        private SqlExpression Render(ScopeFilter filter, IReadOnlyList<SqlIdentifier> qualifier)
        {
            if (filter.Condition is { } condition)
            {
                var column = new ColumnExpression([.. qualifier, Quote(condition.Column)]);

                switch (condition.Operator)
                {
                    case ScopeOperator.Equals:
                        var value = condition.Values.Count > 0 ? condition.Values[0] : null;

                        return value is null
                            ? new IsNullExpression(column)
                            : new BinaryExpression(column, BinaryOperator.Equal, Bind(value));

                    case ScopeOperator.In:
                    case ScopeOperator.NotIn:
                        var negated = condition.Operator == ScopeOperator.NotIn;
                        var values = condition.Values.Where(candidate => candidate is not null).ToArray();
                        var hasNull = condition.Values.Count != values.Length;
                        var members = new List<SqlExpression>();

                        if (values.Length > 0)
                        {
                            members.Add(new InExpression(column, values.Select(Bind).ToArray(), null, negated));
                        }

                        if (hasNull)
                        {
                            members.Add(new IsNullExpression(column, negated));
                        }

                        return members.Count switch
                        {
                            0 => negated ? True() : False(),
                            1 => members[0],
                            _ => new ParenthesizedExpression(Combine(members, negated ? BinaryOperator.And : BinaryOperator.Or)),
                        };

                    default:
                        throw new NotSupportedException($"Unsupported scope operator '{condition.Operator}'.");
                }
            }

            if (filter.All is { Count: > 0 } all)
            {
                return new ParenthesizedExpression(Combine(all.Select(child => Render(child, qualifier)).ToList(), BinaryOperator.And));
            }

            if (filter.Any is { Count: > 0 } any)
            {
                return new ParenthesizedExpression(Combine(any.Select(child => Render(child, qualifier)).ToList(), BinaryOperator.Or));
            }

            return False();
        }

        private SqlExpression Bind(object value)
        {
            var name = "__scope" + _scopeParameters++;
            _scopeValues[name] = value;

            return new ParameterExpression(name);
        }

        private static SqlExpression Conjoin(SqlExpression where, List<SqlExpression> predicates)
        {
            if (predicates.Count == 0)
            {
                return where;
            }

            var members = new List<SqlExpression>();

            if (where is not null)
            {
                members.Add(new ParenthesizedExpression(where));
            }

            members.AddRange(predicates);

            return Combine(members, BinaryOperator.And);
        }

        private static SqlExpression Combine(IReadOnlyList<SqlExpression> members, BinaryOperator op)
        {
            var result = members[0];

            for (var i = 1; i < members.Count; i++)
            {
                result = new BinaryExpression(result, op, members[i]);
            }

            return result;
        }

        private static SqlExpression False() => new BinaryExpression(new LiteralExpression(1L), BinaryOperator.Equal, new LiteralExpression(0L));

        private static SqlExpression True() => new BinaryExpression(new LiteralExpression(1L), BinaryOperator.Equal, new LiteralExpression(1L));

        private IReadOnlyList<SqlIdentifier> GetTableNameParts(string tableName)
        {
            var quotedTableName = dialect.QuoteForTableName(tableName, schema);

            return !string.IsNullOrEmpty(schema) && quotedTableName.Contains('.', StringComparison.Ordinal)
                ? [Quote(schema), Quote(tableName)]
                : [Quote(tableName)];
        }

        private SqlExpression GetLimit(SqlExpression limit, SqlExpression offset)
        {
            if (offset is null || limit is not null)
            {
                return limit;
            }

            return dialect switch
            {
                SqliteDialect => new LiteralExpression(-1L),
                PostgreSqlDialect => new ColumnExpression([new SqlIdentifier("all")]),
                MySqlDialect => new LiteralExpression(18446744073709551610M),
                _ => null,
            };
        }

        private static SqlIdentifier Quote(string value) => new(value, IsQuoted: true);
    }

    private sealed class PlatformSqlDialect(ISqlDialect dialect)
        : Cyqwel.Dialects.SqlDialect(
            dialect.Name,
            GetOpenQuote(dialect),
            GetCloseQuote(dialect),
            GetLimitStyle(dialect))
    {
        public override string RenderLiteral(LiteralExpression literal, SqlGenerationOptions options) =>
            dialect.GetSqlValue(literal.Value);

        public override string RenderFunction(
            FunctionCallExpression function,
            Func<SqlExpression, string> renderExpression,
            SqlGenerationOptions options)
        {
            if (function.Name.Value == RandomFunctionName)
            {
                return dialect.RandomOrderByClause;
            }

            var arguments = function.Arguments.Select(renderExpression).ToArray();
            if (function.IsDistinct && arguments.Length > 0)
            {
                arguments[0] = "DISTINCT " + arguments[0];
            }

            return dialect.RenderMethod(function.Name.Value, arguments);
        }

        public override bool ShouldQuoteIdentifier(SqlIdentifier identifier) => identifier.IsQuoted;

        private static char GetOpenQuote(ISqlDialect dialect) => GetQuotedIdentifier(dialect)[0];

        private static char GetCloseQuote(ISqlDialect dialect) => GetQuotedIdentifier(dialect)[^1];

        private static string GetQuotedIdentifier(ISqlDialect dialect) =>
            dialect.QuoteForColumnName("identifier");

        private static SqlLimitStyle GetLimitStyle(ISqlDialect dialect) =>
            dialect is SqlServerDialect ? SqlLimitStyle.Top : SqlLimitStyle.LimitOffset;
    }
}
