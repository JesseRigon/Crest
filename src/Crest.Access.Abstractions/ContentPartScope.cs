namespace Crest.Access;

/// <summary>
/// Per-part access as scope (docs/permissions.md, ruling 2026-10-10): a module that needs a
/// part of a content type readable by fewer callers than the item registers an
/// <see cref="IScopeProvider"/> whose <see cref="IScopeProvider.Target"/> is
/// <see cref="Target"/> for that type and part. Its rule is evaluated against the item's
/// content index row (<c>ContentItemIndex</c> columns): <see cref="ScopeKind.All"/> shows the
/// part, <see cref="ScopeKind.None"/> leaves it out, a filter shows it on the rows it admits (a semi-join, which needs the store, hides it).
/// A part with no provider follows the item: whoever may view the item reads it. No rule ships;
/// the first comes with the first type that needs one.
/// </summary>
public static class ContentPartScope
{
    public const string Prefix = "ContentPart:";

    public static string Target(string contentType, string part) => $"{Prefix}{contentType}.{part}";

    /// <summary>
    /// Whether the caller's <paramref name="scopes"/> admit <paramref name="part"/> of an item of
    /// <paramref name="contentType"/>, given the item's index columns; true when no provider
    /// speaks for the part.
    /// </summary>
    public static bool Admits(ScopeSet scopes, string contentType, string part, Func<string, object?> column)
    {
        if (!scopes.TryGet(Target(contentType, part), out var rule))
        {
            return true;
        }

        return rule.Kind switch
        {
            ScopeKind.All => true,
            ScopeKind.None => false,
            // A semi-join cannot be read off one row: fail closed.
            _ => !rule.Filter!.HasSelect && rule.Filter.Matches(column),
        };
    }
}
