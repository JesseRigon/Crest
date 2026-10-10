#nullable enable
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace Crest.Queries.Structured;

/// <summary>
/// A saved query as a step list, compiled through the store's dialect into parameterised
/// SQL: no SQL text, so nothing an author writes reaches the statement unbound.
/// </summary>
public sealed class StructuredQuery
{
    public List<QueryParameter> Parameters { get; set; } = [];

    public List<QueryStep> Steps { get; set; } = [];
}

public enum QueryParameterType
{
    String,
    Int,
    Long,
    Decimal,
    Bool,
    DateTime,
    StringArray,
}

/// <summary>A parameter a query declares; callers bind it by name.</summary>
public sealed record QueryParameter(string Name, QueryParameterType Type, bool Required = false, JsonNode? Default = null)
{
    [JsonIgnore]
    public Type ClrType => Type switch
    {
        QueryParameterType.Int => typeof(int),
        QueryParameterType.Long => typeof(long),
        QueryParameterType.Decimal => typeof(decimal),
        QueryParameterType.Bool => typeof(bool),
        QueryParameterType.DateTime => typeof(DateTime),
        QueryParameterType.StringArray => typeof(string[]),
        _ => typeof(string),
    };
}

[JsonPolymorphic(TypeDiscriminatorPropertyName = "step")]
[JsonDerivedType(typeof(FromStep), "from")]
[JsonDerivedType(typeof(JoinStep), "join")]
[JsonDerivedType(typeof(FilterStep), "filter")]
[JsonDerivedType(typeof(ProjectStep), "project")]
[JsonDerivedType(typeof(SortStep), "sort")]
[JsonDerivedType(typeof(PageStep), "page")]
public abstract class QueryStep
{
}

/// <summary>The first step: an index table (<c>ContentItemIndex</c>) or a content type name.</summary>
public sealed class FromStep : QueryStep
{
    public string Source { get; set; } = string.Empty;

    /// <summary>The alias later steps refer to the table by; the source name when unset.</summary>
    public string? Alias { get; set; }
}

public enum JoinKind
{
    Inner,
    Left,
}

/// <summary>Joins another index on a column pair: <see cref="Column"/> of the joined index equals <see cref="ToColumn"/> of an earlier table.</summary>
public sealed class JoinStep : QueryStep
{
    public string Index { get; set; } = string.Empty;

    public string? Alias { get; set; }

    public JoinKind Kind { get; set; }

    /// <summary>A column of the joined index.</summary>
    public string Column { get; set; } = string.Empty;

    /// <summary>A column of an earlier table, as <c>alias.Column</c> or a column of the From table.</summary>
    public string ToColumn { get; set; } = string.Empty;
}

public enum FilterOperator
{
    Equals,
    NotEquals,
    GreaterThan,
    GreaterThanOrEqual,
    LessThan,
    LessThanOrEqual,
    Like,
    In,
    IsNull,
    IsNotNull,
}

/// <summary>A condition on a column against a literal value or a declared parameter.</summary>
public sealed class FilterStep : QueryStep
{
    public string Column { get; set; } = string.Empty;

    public FilterOperator Operator { get; set; }

    /// <summary>A literal, bound as a parameter at run time; ignored when <see cref="Parameter"/> is set.</summary>
    public JsonNode? Value { get; set; }

    /// <summary>The name of a declared parameter whose bound value the condition uses.</summary>
    public string? Parameter { get; set; }
}

public sealed class ProjectStep : QueryStep
{
    public List<ProjectedColumn> Columns { get; set; } = [];
}

public sealed record ProjectedColumn(string Column, string? Alias = null);

public sealed class SortStep : QueryStep
{
    public List<SortColumn> Columns { get; set; } = [];
}

public sealed record SortColumn(string Column, bool Descending = false);

public sealed class PageStep : QueryStep
{
    public int Size { get; set; } = 20;

    /// <summary>A fixed page token, when the query always starts from a given page; the request's token wins.</summary>
    public string? Token { get; set; }
}

/// <summary>A reference to a column in a step: <c>Column</c> or <c>alias.Column</c>.</summary>
public readonly record struct ColumnReference(string? Qualifier, string Name)
{
    public static ColumnReference Parse(string text)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(text);

        var dot = text.IndexOf('.');

        return dot < 0
            ? new ColumnReference(null, text.Trim())
            : new ColumnReference(text[..dot].Trim(), text[(dot + 1)..].Trim());
    }
}

public sealed class StructuredQueryException(string message) : InvalidOperationException(message);
