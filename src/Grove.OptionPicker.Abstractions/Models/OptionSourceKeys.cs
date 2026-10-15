namespace Grove.Models;

/// <summary>
/// Source keys name which provider a picker draws from, and are stored in field
/// settings and on the field itself. Content part list sources are qualified by the list's
/// logical key, so one provider serves every list.
/// </summary>
public static class OptionSourceKeys
{
    /// <summary>The column name meaning "the option's technical key" (the default comparison
    /// column of a dependent filter or a visibility condition).</summary>
    public const string KeyColumn = "Key";

    /// <summary>Provider key for tenant-editable Content Part Lists.</summary>
    public const string ContentPartListProvider = "contentpartlist";

    /// <summary>Builds the source key for an Content part list, e.g.
    /// "contentpartlist:pricing.modifier-kind".</summary>
    public static string ForContentPartList(string listKey) => $"{ContentPartListProvider}:{listKey}";

    /// <summary>Provider key for content items.</summary>
    public const string ContentItemProvider = "contentitem";

    /// <summary>
    /// Builds the source key for content items: of the listed types and of the types with the
    /// listed stereotypes ("contentitem:Article,@Widget"), or of every type without a stereotype
    /// ("contentitem:*").
    /// </summary>
    public static string ForContentItems(IEnumerable<string>? types, IEnumerable<string>? stereotypes = null, bool allTypes = false) =>
        $"{ContentItemProvider}:{ContentItemSourceScope.Format(types, stereotypes, allTypes)}";

    /// <summary>Splits a source key into its provider key and the provider-specific
    /// remainder ("contentpartlist:pricing.side" -> "contentpartlist", "pricing.side").</summary>
    public static (string Provider, string Qualifier) Split(string? sourceKey)
    {
        var value = sourceKey?.Trim() ?? string.Empty;
        var separator = value.IndexOf(':');
        return separator < 0
            ? (value, string.Empty)
            : (value[..separator], value[(separator + 1)..]);
    }
}

/// <summary>
/// What a content item source's qualifier names: types, types by stereotype (<c>@Stereotype</c>),
/// or every type without a stereotype (<c>*</c>).
/// </summary>
public sealed record ContentItemSourceScope(IReadOnlyList<string> Types, IReadOnlyList<string> Stereotypes, bool AllTypes)
{
    public const string All = "*";

    public const char StereotypePrefix = '@';

    public static ContentItemSourceScope Parse(string? qualifier)
    {
        var entries = (qualifier ?? string.Empty)
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var all = entries.Contains(All, StringComparer.Ordinal);
        var stereotypes = entries.Where(entry => entry.Length > 1 && entry[0] == StereotypePrefix).Select(entry => entry[1..]).ToArray();
        var types = entries.Where(entry => entry != All && entry[0] != StereotypePrefix).ToArray();
        return new ContentItemSourceScope(types, stereotypes, all);
    }

    public static string Format(IEnumerable<string>? types, IEnumerable<string>? stereotypes, bool allTypes)
    {
        if (allTypes)
        {
            return All;
        }

        return string.Join(',', (types ?? []).Where(type => !string.IsNullOrWhiteSpace(type)).Select(type => type.Trim())
            .Concat((stereotypes ?? []).Where(stereotype => !string.IsNullOrWhiteSpace(stereotype)).Select(stereotype => StereotypePrefix + stereotype.Trim())));
    }

    /// <summary>Whether the qualifier names nothing (no type, no stereotype, not all).</summary>
    public bool IsEmpty => !AllTypes && Types.Count == 0 && Stereotypes.Count == 0;
}
