using System.Text.Json.Nodes;

namespace Crest.UrlRewriting.Models;

public sealed class InitializingRewriteRuleContext : RewriteRuleContextBase
{
    public JsonNode Data { get; }

    public InitializingRewriteRuleContext(RewriteRule rule, JsonNode data)
        : base(rule)
    {
        Data = data ?? new JsonObject();
    }
}
