using Grove.Data.Documents;

namespace Grove.UrlRewriting.Models;

public sealed class RewriteRulesDocument : Document
{
    public Dictionary<string, RewriteRule> Rules { get; set; } = [];
}
