using Grove.Data.Documents;

namespace Grove.Routes.Models;

public sealed class RewriteRulesDocument : Document
{
    public Dictionary<string, RewriteRule> Rules { get; set; } = [];
}
