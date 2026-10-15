using System.Text.Json.Nodes;

namespace Grove.Routes.Models;

public sealed class InitializingRewriteRuleContext : RewriteRuleContextBase
{
    public JsonNode Data { get; }

    public InitializingRewriteRuleContext(RewriteRule rule, JsonNode data)
        : base(rule)
    {
        Data = data ?? new JsonObject();
    }
}
