using Crest.Queries;

namespace Crest.Data;

public class SQLQueryResults : IQueryResults
{
    public IEnumerable<object> Items { get; set; }
}
