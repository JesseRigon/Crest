namespace Grove.Access;

/// <summary>
/// Per-field-set access as scope (docs/permissions.md): a module that needs a
/// field set of a content type readable by fewer callers than the item registers an
/// <see cref="IScopeProvider"/> whose <see cref="IScopeProvider.Target"/> is
/// <see cref="Target"/> for that type and field set. Its rule is evaluated against the item's
/// content index row (<c>ContentItemIndex</c> columns): <see cref="ScopeKind.All"/> shows the
/// field set, <see cref="ScopeKind.None"/> leaves it out, a filter shows it on the rows it admits (a semi-join, which needs the store, hides it).
/// A field set with no provider follows the item: whoever may view the item reads it. No rule ships;
/// the first comes with the first type that needs one.
/// </summary>
public static class ContentFieldSetScope
{
    public const string Prefix = "ContentFieldSet:";

    public static string Target(string contentType, string fieldSet) => $"{Prefix}{contentType}.{fieldSet}";

    /// <summary>
    /// Whether the caller's <paramref name="scopes"/> admit <paramref name="fieldSet"/> of an item of
    /// <paramref name="contentType"/>, given the item's index columns; true when no provider
    /// speaks for the field set.
    /// </summary>
    public static bool Admits(ScopeSet scopes, string contentType, string fieldSet, Func<string, object?> column)
    {
        if (!scopes.TryGet(Target(contentType, fieldSet), out var rule))
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
