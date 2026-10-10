#nullable enable
using System.Diagnostics.CodeAnalysis;
using Crest.Access;

namespace Crest.Tests.Queries;

/// <summary>Scope sets for tests: a permissive one for parser shape tests, explicit ones for scope tests.</summary>
public static class TestScopes
{
    /// <summary>Every target is readable in full. For tests of SQL shape only; never a runtime default.</summary>
    public static ScopeSet AllowAll { get; } = new("allow-all", new PermissiveRules());

    public static ScopeSet Of(params (string Target, ScopeRule Rule)[] rules) =>
        new("test", rules.ToDictionary(rule => rule.Target, rule => rule.Rule, StringComparer.OrdinalIgnoreCase));

    private sealed class PermissiveRules : IReadOnlyDictionary<string, ScopeRule>
    {
        public ScopeRule this[string key] => ScopeRule.All;

        public IEnumerable<string> Keys => [];

        public IEnumerable<ScopeRule> Values => [];

        public int Count => 0;

        public bool ContainsKey(string key) => true;

        public bool TryGetValue(string key, [MaybeNullWhen(false)] out ScopeRule value)
        {
            value = ScopeRule.All;
            return true;
        }

        public IEnumerator<KeyValuePair<string, ScopeRule>> GetEnumerator() => Enumerable.Empty<KeyValuePair<string, ScopeRule>>().GetEnumerator();

        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();
    }
}
