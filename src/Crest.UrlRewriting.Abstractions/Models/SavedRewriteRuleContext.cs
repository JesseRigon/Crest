namespace Crest.UrlRewriting.Models;

public sealed class SavedRewriteRuleContext : RewriteRuleContextBase
{
    public SavedRewriteRuleContext(RewriteRule rule)
        : base(rule)
    {
    }
}
