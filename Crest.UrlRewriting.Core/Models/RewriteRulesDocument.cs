using Crest.Data.Documents;

namespace Crest.UrlRewriting.Models;

public sealed class RewriteRulesDocument : Document
{
    public Dictionary<string, RewriteRule> Rules { get; set; } = [];
}
