using Grove.Data.Documents;

namespace Grove.Queries.Models;

public sealed class QueriesDocument : Document
{
    public Dictionary<string, Query> Queries { get; init; } = new(StringComparer.OrdinalIgnoreCase);
}
