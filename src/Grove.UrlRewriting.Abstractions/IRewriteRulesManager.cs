using System.Text.Json.Nodes;
using Grove.UrlRewriting.Models;

namespace Grove.UrlRewriting;

public interface IRewriteRulesManager
{
    Task<RewriteRule> NewAsync(string source, JsonNode data = null);

    Task<RewriteValidateResult> ValidateAsync(RewriteRule rule);

    Task<RewriteRule> FindByIdAsync(string id);

    Task SaveAsync(RewriteRule rule);

    Task DeleteAsync(RewriteRule rule);

    Task UpdateAsync(RewriteRule rule, JsonNode data = null);

    Task ResortOrderAsync(int oldOrder, int newOrder);

    Task<IEnumerable<RewriteRule>> GetAllAsync();
}
