namespace Grove.Routes.Models;

public abstract class RewriteRuleContextBase
{
    public RewriteRule Rule { get; }

    public RewriteRuleContextBase(RewriteRule rule)
    {
        ArgumentNullException.ThrowIfNull(rule);

        Rule = rule;
    }
}
