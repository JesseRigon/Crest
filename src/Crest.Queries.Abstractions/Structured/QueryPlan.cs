#nullable enable
using System.Text.Json.Nodes;

namespace Crest.Queries.Structured;

public sealed record PlanColumnRef(string Alias, QueryColumn Column);

/// <summary>A table of the plan: the From table first, then each join in order.</summary>
public sealed record PlanTable(
    string Alias,
    IndexTableDescriptor Table,
    /// <summary>Set when the From step names a content type: the plan reads its published items.</summary>
    string? ContentType,
    JoinKind? Join,
    PlanColumnRef? JoinColumn,
    PlanColumnRef? JoinToColumn)
{
    /// <summary>The column that orders rows stably: <c>DocumentId</c> when the index has one, else <c>Id</c>.</summary>
    public QueryColumn StableColumn => Table.FindColumn("DocumentId") ?? Table.FindColumn("Id")
        ?? throw new StructuredQueryException($"The index '{Table.Name}' has neither a DocumentId nor an Id column.");
}

public sealed record PlanFilter(PlanColumnRef Column, FilterOperator Operator, JsonNode? Value, QueryParameter? Parameter);

public sealed record PlanColumn(PlanColumnRef Source, string Name);

public sealed record PlanSort(PlanColumnRef Column, bool Descending);

/// <summary>
/// A structured query resolved against the index catalog: every table, column and parameter
/// checked, the output columns derived from the index types. What a compiler consumes and
/// what a descriptor reports.
/// </summary>
public sealed class QueryPlan
{
    public const string ContentIndexName = "ContentItemIndex";

    private QueryPlan()
    {
    }

    public IReadOnlyList<PlanTable> Tables { get; private init; } = [];

    public PlanTable From => Tables[0];

    public IReadOnlyList<PlanFilter> Filters { get; private init; } = [];

    public IReadOnlyList<PlanColumn> Columns { get; private init; } = [];

    public IReadOnlyList<PlanSort> Sorts { get; private init; } = [];

    public IReadOnlyList<QueryParameter> Parameters { get; private init; } = [];

    public int? PageSize { get; private init; }

    public string? PageToken { get; private init; }

    /// <summary>The output schema: column names and CLR types.</summary>
    public IReadOnlyList<QueryColumn> OutputColumns =>
        Columns.Select(column => new QueryColumn(column.Name, column.Source.Column.Type)).ToArray();

    public static QueryPlan Build(StructuredQuery query, IIndexTableCatalog catalog)
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(catalog);

        if (query.Steps.Count == 0 || query.Steps[0] is not FromStep from)
        {
            throw new StructuredQueryException("A query starts with a From step.");
        }

        var parameters = query.Parameters
            .GroupBy(parameter => parameter.Name, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .ToArray();

        var tables = new List<PlanTable> { ResolveFrom(from, catalog) };
        var filters = new List<PlanFilter>();
        var projections = new List<PlanColumn>();
        var sorts = new List<PlanSort>();
        int? pageSize = null;
        string? pageToken = null;

        foreach (var step in query.Steps.Skip(1))
        {
            switch (step)
            {
                case FromStep:
                    throw new StructuredQueryException("Only the first step can be a From step.");

                case JoinStep join:
                    tables.Add(ResolveJoin(join, tables, catalog));
                    break;

                case FilterStep filter:
                    filters.Add(ResolveFilter(filter, tables, parameters));
                    break;

                case ProjectStep project:
                    foreach (var projected in project.Columns)
                    {
                        var source = ResolveColumn(projected.Column, tables);
                        projections.Add(new PlanColumn(source, string.IsNullOrWhiteSpace(projected.Alias) ? source.Column.Name : projected.Alias.Trim()));
                    }

                    break;

                case SortStep sort:
                    foreach (var sortColumn in sort.Columns)
                    {
                        sorts.Add(new PlanSort(ResolveColumn(sortColumn.Column, tables), sortColumn.Descending));
                    }

                    break;

                case PageStep page:
                    if (page.Size <= 0)
                    {
                        throw new StructuredQueryException("A page size is a positive number.");
                    }

                    pageSize = page.Size;
                    pageToken = page.Token;
                    break;

                default:
                    throw new StructuredQueryException($"Unknown step '{step.GetType().Name}'.");
            }
        }

        if (projections.Count == 0)
        {
            projections.AddRange(DefaultProjection(tables));
        }

        var duplicate = projections.GroupBy(column => column.Name, StringComparer.OrdinalIgnoreCase).FirstOrDefault(group => group.Count() > 1);

        if (duplicate is not null)
        {
            throw new StructuredQueryException($"The output column '{duplicate.Key}' is projected more than once.");
        }

        return new QueryPlan
        {
            Tables = tables,
            Filters = filters,
            Columns = projections,
            Sorts = sorts,
            Parameters = parameters,
            PageSize = pageSize,
            PageToken = pageToken,
        };
    }

    private static PlanTable ResolveFrom(FromStep from, IIndexTableCatalog catalog)
    {
        if (string.IsNullOrWhiteSpace(from.Source))
        {
            throw new StructuredQueryException("The From step names an index table or a content type.");
        }

        var source = from.Source.Trim();
        var table = catalog.Find(source);

        if (table is not null)
        {
            return new PlanTable(Alias(from.Alias, table.Name), table, null, null, null, null);
        }

        var contentIndex = catalog.Find(ContentIndexName)
            ?? throw new StructuredQueryException($"'{source}' is neither an index table nor a content type the store can read (no '{ContentIndexName}').");

        return new PlanTable(Alias(from.Alias, source), contentIndex, source, null, null, null);
    }

    private static PlanTable ResolveJoin(JoinStep join, List<PlanTable> tables, IIndexTableCatalog catalog)
    {
        var table = catalog.Find(join.Index?.Trim() ?? string.Empty)
            ?? throw new StructuredQueryException($"Unknown index '{join.Index}' in a Join step.");

        var alias = Alias(join.Alias, table.Name);

        if (tables.Any(existing => string.Equals(existing.Alias, alias, StringComparison.OrdinalIgnoreCase)))
        {
            throw new StructuredQueryException($"The alias '{alias}' is used twice; give the join an alias.");
        }

        var column = table.FindColumn(ColumnReference.Parse(join.Column).Name)
            ?? throw new StructuredQueryException($"The index '{table.Name}' has no column '{join.Column}'.");

        var toColumn = ResolveColumn(join.ToColumn, tables);

        return new PlanTable(alias, table, null, join.Kind, new PlanColumnRef(alias, column), toColumn);
    }

    private static PlanFilter ResolveFilter(FilterStep filter, List<PlanTable> tables, QueryParameter[] parameters)
    {
        var column = ResolveColumn(filter.Column, tables);
        QueryParameter? parameter = null;

        if (!string.IsNullOrWhiteSpace(filter.Parameter))
        {
            parameter = parameters.FirstOrDefault(candidate => string.Equals(candidate.Name, filter.Parameter.Trim(), StringComparison.OrdinalIgnoreCase))
                ?? throw new StructuredQueryException($"The filter on '{filter.Column}' names an undeclared parameter '{filter.Parameter}'.");
        }

        var needsValue = filter.Operator is not (FilterOperator.IsNull or FilterOperator.IsNotNull);

        if (needsValue && parameter is null && filter.Value is null)
        {
            throw new StructuredQueryException($"The filter on '{filter.Column}' needs a value or a parameter.");
        }

        if (filter.Operator == FilterOperator.In && parameter is not null && parameter.Type != QueryParameterType.StringArray)
        {
            throw new StructuredQueryException($"An In filter binds a string[] parameter; '{parameter.Name}' is {parameter.Type}.");
        }

        return new PlanFilter(column, filter.Operator, filter.Value, parameter);
    }

    private static PlanColumnRef ResolveColumn(string text, List<PlanTable> tables)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            throw new StructuredQueryException("A column reference is empty.");
        }

        var reference = ColumnReference.Parse(text);

        if (reference.Qualifier is not null)
        {
            var table = tables.FirstOrDefault(candidate => string.Equals(candidate.Alias, reference.Qualifier, StringComparison.OrdinalIgnoreCase))
                ?? throw new StructuredQueryException($"Unknown table alias '{reference.Qualifier}' in '{text}'.");

            var column = table.Table.FindColumn(reference.Name)
                ?? throw new StructuredQueryException($"The index '{table.Table.Name}' has no column '{reference.Name}'.");

            return new PlanColumnRef(table.Alias, column);
        }

        var matches = tables
            .Select(table => (table, column: table.Table.FindColumn(reference.Name)))
            .Where(match => match.column is not null)
            .ToArray();

        if (matches.Length == 0)
        {
            throw new StructuredQueryException($"No table of the query has a column '{reference.Name}'.");
        }

        var chosen = matches.Length == 1
            ? matches[0]
            : matches.FirstOrDefault(match => ReferenceEquals(match.table, tables[0]));

        if (chosen.table is null)
        {
            throw new StructuredQueryException($"The column '{reference.Name}' is ambiguous; qualify it with a table alias.");
        }

        return new PlanColumnRef(chosen.table.Alias, chosen.column!);
    }

    private static IEnumerable<PlanColumn> DefaultProjection(List<PlanTable> tables)
    {
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var table in tables)
        {
            foreach (var column in table.Table.Columns)
            {
                var name = names.Add(column.Name) ? column.Name : $"{table.Alias}_{column.Name}";
                names.Add(name);
                yield return new PlanColumn(new PlanColumnRef(table.Alias, column), name);
            }
        }
    }

    private static string Alias(string? alias, string fallback) =>
        string.IsNullOrWhiteSpace(alias) ? fallback : alias.Trim();
}
