using System.Text.Json.Nodes;

namespace Grove.Queries;

public sealed class InitializingQueryContext : DataQueryContextBase
{
    public InitializingQueryContext(Query query, JsonNode data = null)
        : base(query, data)
    {
    }
}
