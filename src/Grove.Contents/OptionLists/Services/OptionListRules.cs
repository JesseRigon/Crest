namespace Grove.Services;

using Grove.Models;

/// <summary>
/// The pure rules behind Option lists - key normalization, uniqueness, seed layering
/// and effective-list projection. Kept free of Grove content plumbing so the
/// behaviour that matters (a tenant's relabel surviving a module reseed, a hidden
/// option staying resolvable) is directly unit-testable.
/// </summary>
public static class OptionListRules
{
    /// <summary>Keys are compared case-insensitively and stored trimmed; comparing
    /// them any other way would make "markup" and "Markup" two different options in
    /// one set.</summary>
    public static readonly StringComparer KeyComparer = StringComparer.OrdinalIgnoreCase;

    public static string NormalizeKey(string? key) => key?.Trim() ?? string.Empty;

    /// <summary>Categories are labels, not keys: trimmed as typed, and blank means
    /// the option belongs to the default Uncategorized bucket. Never returns
    /// null or empty.</summary>
    public static string NormalizeCategory(string? category)
    {
        var value = category?.Trim();
        return string.IsNullOrEmpty(value) ? OptionCategories.Uncategorized : value;
    }

    /// <summary>A parent is an option KEY: normalized like one; blank means the top level (null).</summary>
    public static string? NormalizeParent(string? parent)
    {
        var value = NormalizeKey(parent);
        return value.Length == 0 ? null : value;
    }

    /// <summary>
    /// Why <paramref name="parent"/> cannot be <paramref name="optionKey"/>'s parent, or
    /// null: the parent must be another option of the same list, and placing the option
    /// there must not make it its own ancestor. Blank (the top level) is always valid.
    /// </summary>
    public static string? ValidateParent(IReadOnlyList<GroveOptionModel> options, string optionKey, string? parent)
    {
        var normalized = NormalizeParent(parent);
        if (normalized is null)
        {
            return null;
        }

        var byKey = options.GroupBy(option => option.Key, KeyComparer).ToDictionary(group => group.Key, group => group.First(), KeyComparer);
        if (!byKey.ContainsKey(normalized))
        {
            return $"'{normalized}' is not an option of this list.";
        }

        var seen = new HashSet<string>(KeyComparer);
        for (var current = normalized; current is not null; current = byKey.TryGetValue(current, out var option) ? option.Parent : null)
        {
            if (KeyComparer.Equals(current, optionKey) || !seen.Add(current))
            {
                return $"'{optionKey}' cannot sit under '{normalized}': that would make it its own ancestor.";
            }
        }

        return null;
    }

    /// <summary>The ancestors of an option, top first (empty for a top-level option).
    /// Stops at a missing parent or a cycle rather than failing.</summary>
    public static IReadOnlyList<GroveOptionModel> Ancestors(IReadOnlyDictionary<string, GroveOptionModel> byKey, GroveOptionModel option)
    {
        var chain = new List<GroveOptionModel>();
        var seen = new HashSet<string>(KeyComparer) { option.Key };
        for (var current = option.Parent; current is not null && byKey.TryGetValue(current, out var parent) && seen.Add(parent.Key); current = parent.Parent)
        {
            chain.Add(parent);
        }

        chain.Reverse();
        return chain;
    }

    /// <summary>The options directly under <paramref name="parent"/> (the top level when
    /// blank), in the list's order, each saying whether it has options below it.</summary>
    public static IEnumerable<OptionNode> Children(IReadOnlyList<GroveOptionModel> options, string? parent)
    {
        var normalized = NormalizeParent(parent);
        var withChildren = options.Where(option => option.Parent is not null).Select(option => option.Parent!).ToHashSet(KeyComparer);
        return options
            .Where(option => KeyComparer.Equals(option.Parent ?? string.Empty, normalized ?? string.Empty))
            .Select(option => new OptionNode(option, withChildren.Contains(option.Key)));
    }

    /// <summary>
    /// Validates a full manual reorder: the proposed keys must be exactly the list's
    /// current keys (same set, no omissions, no strangers, no duplicates). Anything
    /// less would let a partial payload silently collapse positions.
    /// </summary>
    public static OptionKeyValidation ValidateReorder(IEnumerable<string> existingKeys, IReadOnlyList<string>? proposedKeys)
    {
        var existing = new HashSet<string>(existingKeys.Select(NormalizeKey), KeyComparer);
        var proposed = (proposedKeys ?? []).Select(NormalizeKey).ToArray();

        if (proposed.Length != existing.Count || proposed.Distinct(KeyComparer).Count() != proposed.Length)
        {
            return OptionKeyValidation.Invalid("A reorder must name every option in the list exactly once.");
        }

        return proposed.All(existing.Contains)
            ? OptionKeyValidation.Valid
            : OptionKeyValidation.Invalid("A reorder may only name options that are in the list.");
    }

    /// <summary>
    /// Validates a key about to be written into <paramref name="existingKeys"/>.
    /// Grove has no unique index for a content field, so uniqueness is enforced
    /// here, on write.
    /// </summary>
    /// <param name="key">The candidate key.</param>
    /// <param name="existingKeys">Keys already present in the set.</param>
    /// <param name="currentKey">The key being replaced, when editing an existing
    /// option - lets an option keep its own key.</param>
    public static OptionKeyValidation ValidateKey(string? key, IEnumerable<string> existingKeys, string? currentKey = null)
    {
        var candidate = NormalizeKey(key);
        if (candidate.Length == 0)
        {
            return OptionKeyValidation.Invalid("A key is required.");
        }

        // The key travels in URLs and is compared in code; keep it to a predictable
        // shape rather than accepting arbitrary display text.
        foreach (var character in candidate)
        {
            if (!char.IsLetterOrDigit(character) && character is not ('.' or '-' or '_'))
            {
                return OptionKeyValidation.Invalid("A key may only contain letters, digits, dot, dash and underscore.");
            }
        }

        var current = NormalizeKey(currentKey);
        if (current.Length > 0 && KeyComparer.Equals(candidate, current))
        {
            return OptionKeyValidation.Valid;
        }

        return existingKeys.Any(existing => KeyComparer.Equals(NormalizeKey(existing), candidate))
            ? OptionKeyValidation.Invalid($"The key '{candidate}' is already used in this list.")
            : OptionKeyValidation.Valid;
    }

    /// <summary>
    /// Orders options in the list's MANUAL order: Position, ties broken by display
    /// text (current culture). This is the list's only intrinsic order - any other
    /// sort (alphabetical, categorized, by key) is an INSTANCE parameter applied by
    /// the consuming picker via its SortColumns, not list data. Hidden options keep
    /// their place - callers decide whether to drop them (see
    /// <see cref="Selectable"/>), because history must still resolve them.
    /// </summary>
    public static IReadOnlyList<GroveOptionModel> Ordered(IEnumerable<GroveOptionModel> options) =>
        [.. options
            .OrderBy(option => option.Position)
            .ThenBy(option => option.DisplayText, StringComparer.CurrentCultureIgnoreCase)];

    /// <summary>The options an editor should offer: everything not hidden, in the
    /// order they arrive - the model's options already carry the list's configured
    /// sort, so re-sorting here would override it.</summary>
    public static IReadOnlyList<GroveOptionModel> Selectable(IEnumerable<GroveOptionModel> options) =>
        [.. options.Where(option => !option.Hidden)];

    /// <summary>Whether a lock value means the lock is active, regardless of who
    /// placed it.</summary>
    public static bool LockActive(string? lockSource) =>
        !string.IsNullOrEmpty(lockSource)
        && !string.Equals(lockSource, OptionListLockSources.None, StringComparison.OrdinalIgnoreCase);

    /// <summary>The category vocabulary of a data-locked list: the distinct
    /// categories its options already carry. Under a data lock this set is frozen -
    /// a tenant-added option must pick from it.</summary>
    public static IReadOnlyList<string> CategoryVocabulary(IEnumerable<GroveOptionModel> options) =>
        [.. options
            .Select(option => NormalizeCategory(option.Category))
            .Distinct(StringComparer.CurrentCultureIgnoreCase)
            .OrderBy(category => category, StringComparer.CurrentCultureIgnoreCase)];

    /// <summary>
    /// Validates the category of an option being ADDED to a list. Unlocked lists take
    /// any category (blank files under Uncategorized); a data-locked list only takes
    /// one of its existing categories, so logic keyed on the category set still
    /// covers every tenant addition.
    /// </summary>
    public static OptionKeyValidation ValidateAddedCategory(OptionListModel list, string? category)
    {
        if (!LockActive(list.DataLock))
        {
            return OptionKeyValidation.Valid;
        }

        var normalized = NormalizeCategory(category);
        var vocabulary = CategoryVocabulary(list.Options);
        return vocabulary.Any(existing => string.Equals(existing, normalized, StringComparison.CurrentCultureIgnoreCase))
            ? OptionKeyValidation.Valid
            : OptionKeyValidation.Invalid(
                $"This list's categories are locked. Pick one of: {string.Join(", ", vocabulary)}.");
    }

    /// <summary>
    /// Layers a module's seed declaration over what the tenant currently has, and
    /// returns what should change. Existing options are matched BY KEY and never
    /// overwritten - a tenant's relabel or hide survives every reseed, which is the
    /// whole point of provenance. Only genuinely new keys are added.
    /// </summary>
    public static OptionSeedPlan PlanSeed(OptionListSeed seed, IEnumerable<GroveOptionModel> existing)
    {
        var present = new HashSet<string>(existing.Select(option => NormalizeKey(option.Key)), KeyComparer);
        var additions = new List<OptionSeed>();
        foreach (var option in seed.Options)
        {
            var key = NormalizeKey(option.Key);
            if (key.Length == 0 || !present.Add(key))
            {
                continue;
            }

            additions.Add(option with { Key = key });
        }

        return new OptionSeedPlan(additions);
    }
}

/// <summary>What a reseed would add. Empty additions means the seed is fully applied
/// and reseeding is a no-op, so module seeding stays idempotent.</summary>
public sealed record OptionSeedPlan(IReadOnlyList<OptionSeed> Additions)
{
    public bool HasChanges => Additions.Count > 0;
}
