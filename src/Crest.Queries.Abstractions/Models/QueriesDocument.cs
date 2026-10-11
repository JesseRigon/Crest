using Crest.Data.Documents;

namespace Crest.Queries.Models;

public sealed class QueriesDocument : Document
{
    public Dictionary<string, Query> Queries { get; init; } = new(StringComparer.OrdinalIgnoreCase);
}
