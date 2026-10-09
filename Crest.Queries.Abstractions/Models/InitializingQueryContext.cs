using System.Text.Json.Nodes;

namespace Crest.Queries;

public sealed class InitializingQueryContext : DataQueryContextBase
{
    public InitializingQueryContext(Query query, JsonNode data = null)
        : base(query, data)
    {
    }
}
