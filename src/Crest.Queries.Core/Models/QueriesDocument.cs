using Crest.Data.Documents;

namespace Crest.Queries.Core.Models;

public sealed class QueriesDocument : Document
{
    public Dictionary<string, Query> Queries { get; init; } = new(StringComparer.OrdinalIgnoreCase);
}
