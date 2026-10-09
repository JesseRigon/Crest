namespace Crest.UrlRewriting.Models;

public sealed class ValidatingRewriteRuleContext : RewriteRuleContextBase
{
    public RewriteValidateResult Result { get; } = new();

    public ValidatingRewriteRuleContext(RewriteRule rule)
        : base(rule)
    {
    }
}
