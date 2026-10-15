using System.Text.Json.Nodes;

namespace Grove.Queries;

public sealed class UpdatingQueryContext : DataQueryContextBase
{
    public UpdatingQueryContext(Query query, JsonNode data = null)
        : base(query, data)
    {
    }
}
