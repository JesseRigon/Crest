using Crest.DisplayManagement;
using Crest.UrlRewriting.Models;

namespace Crest.UrlRewriting.ViewModels;

public class RewriteRuleEntry
{
    public RewriteRule Rule { get; set; }

    public IShape Shape { get; set; }
}
