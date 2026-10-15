using System.Text.Json.Nodes;

namespace Grove.Data.Queries;

public abstract class DataQueryContextBase : QueryContextBase
{
    public JsonNode Data { get; }

    public DataQueryContextBase(Query query, JsonNode data = null)
        : base(query)
    {
        Data = data ?? new JsonObject();
    }
}
