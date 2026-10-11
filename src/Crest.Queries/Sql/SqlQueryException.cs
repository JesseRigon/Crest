#nullable enable
namespace Crest.Queries.Sql;

/// <summary>A SQL template that does not parse, is refused by the rewriter, or cannot serve what the query asks.</summary>
public sealed class SqlQueryException(IReadOnlyList<string> messages)
    : InvalidOperationException(string.Join(" ", messages))
{
    public IReadOnlyList<string> Messages { get; } = messages;
}
