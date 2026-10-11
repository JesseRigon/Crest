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

    /// <summary>The parameter a paged statement binds to the number of rows to skip.</summary>
    public const string SkipParameter = "__skip";

    /// <summary>The parameter a paged statement binds to the number of rows to take.</summary>
    public const string TakeParameter = "__take";

    // The shells a page and a count are built in: the rewritten statement becomes the derived
    // table, its WITH is hoisted onto the shell (SQL Server allows no WITH in a derived table).
    private static readonly SelectStatement PageShell = ParseShell($"SELECT * FROM (SELECT 1) q LIMIT @{TakeParameter} OFFSET @{SkipParameter}");

    private static readonly SelectStatement OrderedPageShell = ParseShell($"SELECT * FROM (SELECT 1) q ORDER BY (SELECT NULL) LIMIT @{TakeParameter} OFFSET @{SkipParameter}");

    private static readonly SelectStatement CountShell = ParseShell("SELECT COUNT(*) FROM (SELECT 1) q");

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
        if (!TryRewrite(sql, schema, dialect, tablePrefix, parameters, scopes, out var rewritten, out messages))
        {
            query = null;
            return false;
        }

        query = new PlatformSqlDialect(dialect).Generate(rewritten, GenerationOptions) + ";";
        return true;
    }

    /// <summary>
    /// Parses and scopes a template like <see cref="TryParse"/>, then wraps its one statement
    /// as a derived table: a page (<c>@__skip</c>, <c>@__take</c>) and a count of all its rows.
    /// The statement's own ORDER BY orders the page on every provider but SQL Server, where an
    /// ORDER BY inside a derived table is kept (with OFFSET 0 ROWS) but not guaranteed to order
    /// the outer page.
    /// </summary>
    public static bool TryParsePage(
        string sql,
        string schema,
        ISqlDialect dialect,
        string tablePrefix,
        IDictionary<string, object> parameters,
        ScopeSet scopes,
        out string pageQuery,
        out string countQuery,
        out IEnumerable<string> messages)
    {
        pageQuery = null;
        countQuery = null;

        if (!TryRewrite(sql, schema, dialect, tablePrefix, parameters, scopes, out var rewritten, out messages))
        {
            return false;
        }

        if (rewritten is not SqlDocument { Statements: [SqlQuery statement] })
        {
            messages = ["Only one statement is allowed."];
            return false;
        }

        var isSqlServer = dialect is SqlServerDialect;
        var generator = new PlatformSqlDialect(dialect);

        pageQuery = generator.Generate(new SqlDocument(Wrap(isSqlServer ? OrderedPageShell : PageShell, statement, isSqlServer)), GenerationOptions) + ";";
        countQuery = generator.Generate(new SqlDocument(Wrap(CountShell, statement, isSqlServer)), GenerationOptions) + ";";
        return true;
    }

    private static SelectStatement Wrap(SelectStatement shell, SqlQuery statement, bool isSqlServer)
    {
        var (ctes, isRecursive, inner) = statement switch
        {
            SelectStatement select => (select.CommonTableExpressions, select.IsRecursive, (SqlQuery)(select with { CommonTableExpressions = null, IsRecursive = false })),
            SetOperationStatement set => (set.CommonTableExpressions, set.IsRecursive, set with { CommonTableExpressions = null, IsRecursive = false }),
            ValuesStatement values => (values.CommonTableExpressions, values.IsRecursive, values with { CommonTableExpressions = null, IsRecursive = false }),
            _ => throw new NotSupportedException($"The statement '{statement.GetType().Name}' cannot be paged."),
        };

        // SQL Server refuses an ORDER BY in a derived table unless it also pages.
        if (isSqlServer)
        {
            inner = inner switch
            {
                SelectStatement { OrderBy.Count: > 0, Limit: null, Offset: null, Top: null } select => select with { Offset = new LiteralExpression(0L) },
                SetOperationStatement { OrderBy.Count: > 0, Limit: null, Offset: null } set => set with { Offset = new LiteralExpression(0L) },
                _ => inner,
            };
        }

        var derived = (DerivedTable)shell.From;

        return shell with
        {
            From = derived with { Query = inner },
            CommonTableExpressions = ctes is { Count: > 0 } ? ctes : null,
            IsRecursive = isRecursive,
        };
    }

    private static SelectStatement ParseShell(string sql) =>
        ParserDialect.TryParse(sql, out var document, out var error) && document!.Statements is [SelectStatement shell]
            ? shell
            : throw new InvalidOperationException($"The paging shell '{sql}' does not parse: {error?.Message}");

    private static bool TryRewrite(
        string sql,
        string schema,
        ISqlDialect dialect,
        string tablePrefix,
        IDictionary<string, object> parameters,
        ScopeSet scopes,
        out SqlNode rewritten,
        out IEnumerable<string> messages)
    {
        ArgumentNullException.ThrowIfNull(scopes);

        rewritten = null;

        if (ContainsLiquid(sql))
        {
            messages = [LiquidNotAllowedMessage];
            return false;
        }

        if (!ParserDialect.TryParse(sql, out var document, out var error))
        {
            messages = error is null
                ? ["Parse error: Unknown parsing error"]
                : [$"Parse error: {error.Message} at position {error.Offset}"];
            return false;
        }

        if (ContainsNonQueryStatement(document!))
        {
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
                names.TableAliases);

            rewritten = rewriter.Visit(document);
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
            messages = [exception.Message];
            return false;
        }
        catch (Exception exception)
        {
            messages = ["Unexpected error: " + exception.Message];
            return false;
        }
    }

    // SELECT ... INTO is not in the parser's grammar (the select AST has no INTO target), so it
    // never parses; every statement that does parse and is not a query is refused here.
    private static bool ContainsNonQueryStatement(SqlNode node) =>
        node.DescendantsAndSelf().OfType<SqlStatement>().Any(static statement => statement is not SqlQuery);

    /// <summary>
    /// The functions a template may call. Anything else (file, network, catalog or admin
    /// functions of a provider) is refused before the statement reaches the database.
    /// </summary>
    private static readonly HashSet<string> AllowedFunctions = new(StringComparer.OrdinalIgnoreCase)
    {
        // Aggregates.
        "count", "sum", "avg", "min", "max", "group_concat", "string_agg",
        // Window functions.
        "row_number", "rank", "dense_rank", "ntile", "lag", "lead", "first_value", "last_value",
        // Strings.
        "lower", "upper", "length", "len", "char_length", "substr", "substring", "trim", "ltrim", "rtrim",
        "replace", "concat", "concat_ws", "left", "right", "instr", "charindex", "position",
        // Nulls and types.
        "coalesce", "ifnull", "isnull", "nullif", "cast", "convert",
        // Numbers.
        "abs", "round", "floor", "ceiling", "ceil",
        // Dates, including the parts the store's dialect translates (second, minute, hour).
        "date", "datetime", "strftime", "julianday", "getdate", "getutcdate", "now", "current_timestamp",
        "dateadd", "datediff", "datepart", "year", "month", "day", "hour", "minute", "second", "date_trunc", "extract",
        // Random.
        "random", "rand", "newid",
        // Emitted by the rewriter itself for ORDER BY random().
        RandomFunctionName,
    };

    private sealed class SqlNameCollector : SqlVisitor
    {
        public HashSet<string> TableAliases { get; } = [];

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
    }

    private sealed class PlatformSqlRewriter(
        string schema,
        string tablePrefix,
        ISqlDialect dialect,
        IDictionary<string, object> parameters,
        ScopeSet scopes,
        HashSet<string> tableAliases) : SqlRewriter
    {
        private int _scopeParameters;
        private int _scopeSelects;
        private readonly IDictionary<string, object> _scopeValues = parameters ?? new Dictionary<string, object>();

        // The CTE names in scope, one frame per WITH being visited: a name resolves to a CTE
        // only inside the statement that declares it, never document-wide.
        private readonly Stack<HashSet<string>> _commonTableExpressions = new();

        private bool IsCommonTableExpression(string name) =>
            _commonTableExpressions.Any(frame => frame.Contains(name));

        protected override SqlNode VisitSelect(SelectStatement node)
        {
            if (node.CommonTableExpressions is not { Count: > 0 })
            {
                return ScopeSelect(node);
            }

            return WithCommonTableExpressions(
                node.CommonTableExpressions,
                node.IsRecursive,
                () => ScopeSelect(node with { CommonTableExpressions = null }),
                (rewritten, ctes) => ((SelectStatement)rewritten) with { CommonTableExpressions = ctes });
        }

        protected override SqlNode VisitSetOperation(SetOperationStatement node)
        {
            if (node.CommonTableExpressions is not { Count: > 0 })
            {
                return LimitSetOperation(node);
            }

            return WithCommonTableExpressions(
                node.CommonTableExpressions,
                node.IsRecursive,
                () => LimitSetOperation(node with { CommonTableExpressions = null }),
                (rewritten, ctes) => ((SetOperationStatement)rewritten) with { CommonTableExpressions = ctes });
        }

        protected override SqlNode VisitValues(ValuesStatement node)
        {
            if (node.CommonTableExpressions is not { Count: > 0 })
            {
                return base.VisitValues(node);
            }

            return WithCommonTableExpressions(
                node.CommonTableExpressions,
                node.IsRecursive,
                () => base.VisitValues(node with { CommonTableExpressions = null }),
                (rewritten, ctes) => ((ValuesStatement)rewritten) with { CommonTableExpressions = ctes });
        }

        /// <summary>
        /// Visits the CTEs of a WITH, then the statement, with the CTE names in scope. A
        /// non-recursive CTE's body is visited before its own name is in scope, so a body that
        /// reads a table of the same name reads (and is scoped as) the real table; a recursive
        /// WITH brings every name into scope first.
        /// </summary>
        private SqlNode WithCommonTableExpressions(
            IReadOnlyList<CommonTableExpression> ctes,
            bool isRecursive,
            Func<SqlNode> visitStatement,
            Func<SqlNode, IReadOnlyList<CommonTableExpression>, SqlNode> attach)
        {
            var frame = new HashSet<string>(StringComparer.Ordinal);
            _commonTableExpressions.Push(frame);

            try
            {
                if (isRecursive)
                {
                    foreach (var cte in ctes)
                    {
                        frame.Add(cte.Name.Value);
                    }
                }

                var rewrittenCtes = new CommonTableExpression[ctes.Count];

                for (var i = 0; i < ctes.Count; i++)
                {
                    rewrittenCtes[i] = (CommonTableExpression)Visit(ctes[i]);
                    frame.Add(ctes[i].Name.Value);
                }

                return attach(visitStatement(), rewrittenCtes);
            }
            finally
            {
                _commonTableExpressions.Pop();
            }
        }

        protected override SqlNode VisitFunctionCall(FunctionCallExpression node)
        {
            if (!AllowedFunctions.Contains(node.Name.Value))
            {
                throw new NotSupportedException($"The function '{node.Name.Value}' is not allowed.");
            }

            return base.VisitFunctionCall(node);
        }

        private SqlNode ScopeSelect(SelectStatement node)
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

        private SqlNode LimitSetOperation(SetOperationStatement node)
        {
            var rewritten = (SetOperationStatement)base.VisitSetOperation(node);
            var limit = GetLimit(rewritten.Limit, rewritten.Offset);

            return ReferenceEquals(limit, rewritten.Limit)
                ? rewritten
                : rewritten with { Limit = limit };
        }

        protected override SqlNode VisitTableName(TableName node)
        {
            // The tenant's prefix and schema are applied here; a schema or database part written
            // in the template would reach past them.
            if (node.Parts.Count != 1)
            {
                throw new NotSupportedException("Only a single-part table name is allowed.");
            }

            var name = node.Parts[0].Value;

            return node with
            {
                Parts = IsCommonTableExpression(name) ? [Quote(name)] : GetTableNameParts(tablePrefix + name),
            };
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
            else if (IsCommonTableExpression(qualifier.Value))
            {
                parts.Add(Quote(qualifier.Value));
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
            if (parameters is null)
            {
                return node;
            }

            // A bound value matches its parameter ignoring case; the statement then names it as
            // it was bound, so a provider that binds by exact name finds it.
            var bound = parameters.Keys.FirstOrDefault(key => string.Equals(key, node.Name, StringComparison.Ordinal))
                ?? parameters.Keys.FirstOrDefault(key => string.Equals(key, node.Name, StringComparison.OrdinalIgnoreCase));

            if (bound is null)
            {
                parameters[node.Name] = node.DefaultValue switch
                {
                    LiteralExpression { Value: bool or string or decimal or long } literal => literal.Value,
                    null => throw new InvalidOperationException($"Missing parameter: {node.Name}"),
                    _ => throw new InvalidOperationException("Unsupported default parameter value type"),
                };

                return node;
            }

            return string.Equals(bound, node.Name, StringComparison.Ordinal) ? node : node with { Name = bound };
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

                case DerivedTable when rewritten is DerivedTable:
                    // A derived table is a select of its own and was scoped when visited.
                    return rewritten;

                default:
                    throw new NotSupportedException($"The table source '{original.GetType().Name}' is not supported.");
            }
        }

        private SqlExpression ScopePredicate(NamedTable table)
        {
            if (table.Name.Parts.Count != 1)
            {
                throw new NotSupportedException("Only a single-part table name is allowed.");
            }

            var name = table.Name.Parts[0].Value;

            if (IsCommonTableExpression(name))
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

                    case ScopeOperator.IsNull:
                        return new IsNullExpression(column);

                    case ScopeOperator.LessThan:
                    case ScopeOperator.LessThanOrEqual:
                    case ScopeOperator.GreaterThan:
                    case ScopeOperator.GreaterThanOrEqual:
                        var operand = condition.Values.Count > 0 ? condition.Values[0] : null;

                        // A comparison with null admits nothing, as in SQL.
                        return operand is null
                            ? False()
                            : new BinaryExpression(column, condition.Operator switch
                            {
                                ScopeOperator.LessThan => BinaryOperator.LessThan,
                                ScopeOperator.LessThanOrEqual => BinaryOperator.LessThanOrEqual,
                                ScopeOperator.GreaterThan => BinaryOperator.GreaterThan,
                                _ => BinaryOperator.GreaterThanOrEqual,
                            }, Bind(operand is DateTimeOffset offset ? offset.UtcDateTime : operand));

                    case ScopeOperator.InSelect:
                    case ScopeOperator.NotInSelect:
                        // The selected table is part of the scope, written after the template
                        // was visited: it is never itself scoped, and it gets the tenant's prefix
                        // and an alias of its own so its columns never resolve to the outer table.
                        var select = condition.RequireSelect();
                        IReadOnlyList<SqlIdentifier> inner = [new SqlIdentifier("__scope_select" + _scopeSelects++)];
                        var query = new SelectStatement(
                            [new SelectItem(new ColumnExpression([.. inner, Quote(select.Column)]))],
                            new NamedTable(new TableName(GetTableNameParts(tablePrefix + select.Table)), inner[0]),
                            Render(select.Filter, inner));

                        return new InExpression(column, query, condition.Operator == ScopeOperator.NotInSelect);

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
