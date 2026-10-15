using Grove.DisplayManagement;
using Grove.UrlRewriting.Models;

namespace Grove.UrlRewriting.ViewModels;

public class RewriteRuleEntry
{
    public RewriteRule Rule { get; set; }

    public IShape Shape { get; set; }
}
