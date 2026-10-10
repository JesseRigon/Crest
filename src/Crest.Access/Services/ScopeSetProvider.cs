namespace Crest.Access.Services;

/// <summary>
/// Builds a caller's <see cref="ScopeSet"/> once per request (the provider is scoped), conjoining
/// every provider registered for a target. Nothing is kept across requests: a scope is part of
/// the one access calculation a request makes, handed down with the caller and discarded with
/// it (docs/access.md › One calculation per request).
/// </summary>
public sealed class ScopeSetProvider(IEnumerable<IScopeProvider> providers) : IScopeSetProvider
{
    private readonly Dictionary<string, ScopeSet> _built = new(StringComparer.Ordinal);

    public async Task<ScopeSet> GetAsync(CallerContext caller, CancellationToken cancellationToken = default)
    {
        var key = caller.Tenant + ':' + caller.ScopeSignature;
        if (_built.TryGetValue(key, out var existing))
        {
            return existing;
        }

        var rules = new Dictionary<string, ScopeRule>(StringComparer.OrdinalIgnoreCase);
        foreach (var group in providers.GroupBy(provider => provider.Target, StringComparer.OrdinalIgnoreCase))
        {
            ScopeRule? rule = null;
            foreach (var provider in group)
            {
                var built = await provider.BuildAsync(caller, cancellationToken);
                rule = rule is null ? built : rule.And(built);
            }

            rules[group.Key] = rule ?? ScopeRule.None;
        }

        var set = new ScopeSet(caller.ScopeSignature, rules);
        _built[key] = set;
        return set;
    }
}
