namespace Crest.Models;

/// <summary>An Content part list as callers see it. Options arrive in the list's MANUAL
/// order (Position); any other ordering is an instance concern - the consuming
/// picker's SortColumns. <paramref name="DataLock"/> and <paramref name="EditLock"/>
/// carry who placed each lock (<see cref="ContentPartListLockSources"/>), "None"
/// when unlocked.</summary>
public sealed record CrestContentPartListModel(
    string ContentItemId,
    string Key,
    string DisplayText,
    string Source,
    string DataLock,
    string EditLock,
    CrestOptionModel[] Options,
    CrestOptionFieldModel[]? Fields = null,
    string OptionContentType = "Option");

/// <summary>A custom data field the list's option content type carries beyond the
/// standard Key/Label/Plural/Category/Value surface. Fields are created at runtime
/// through the Content Parts screen; <paramref name="PartName"/> is where the field
/// lives on the option type (the write path needs it), <paramref name="Type"/> is the
/// field type name (TextField, NumericField, ...), and
/// <paramref name="DataLocked"/> carries the admin's per-field lock designation
/// (see <see cref="Crest.Settings.CrestOptionFieldSettings"/>).</summary>
public sealed record CrestOptionFieldModel(
    string Name,
    string DisplayName,
    string PartName,
    string Type,
    bool DataLocked);

/// <summary>A single option. <paramref name="Key"/> is what code compares;
/// <paramref name="DisplayText"/> is what humans read. <paramref name="Category"/>
/// is never null - uncategorized options carry
/// <see cref="OptionCategories.Uncategorized"/>.
/// <paramref name="DisplayTextPlural"/> is null when the option has no distinct
/// plural - readers fall back to <paramref name="DisplayText"/>.
/// <paramref name="Fields"/> holds the option's custom data field values (keyed by
/// field name, stringified), null when the option type declares no custom fields.
/// <paramref name="Parent"/> is the key of the option this one sits under in a list
/// arranged in levels; null for top-level options and flat lists.</summary>
public sealed record CrestOptionModel(
    string ContentItemId,
    string Key,
    string DisplayText,
    string Source,
    bool Hidden,
    int Position,
    string Category,
    string? DisplayTextPlural = null,
    string? Value = null,
    IReadOnlyDictionary<string, string?>? Fields = null,
    string? Parent = null);

/// <summary>A module's declaration of one option it ships in a set. A null
/// <paramref name="Category"/> seeds as Uncategorized; a null
/// <paramref name="DisplayTextPlural"/> means the singular label serves both; a null
/// <paramref name="Value"/> means the key is the option's only machine datum.</summary>
public sealed record OptionSeed(string Key, string DisplayText, int Position = 0, string? Category = null, string? DisplayTextPlural = null, string? Value = null, string? Parent = null);

/// <summary>A module's declaration of a set it owns, with its base options. The two
/// lock flags are the developer's contract and are RE-ASSERTED on every reseed (they
/// are the one thing a seed owns outright - there is no tenant edit of a module lock
/// to preserve). Options seeded with the default Position 0 receive sequential
/// positions in seed order, so the manual order starts meaningful.</summary>
public sealed record ContentPartListSeed(
    string Key,
    string DisplayText,
    IReadOnlyList<OptionSeed> Options,
    bool DataLock = false,
    bool EditLock = false);

/// <summary>Outcome of validating a key before it is written.</summary>
public sealed record OptionKeyValidation(bool IsValid, string? Error)
{
    public static readonly OptionKeyValidation Valid = new(true, null);

    public static OptionKeyValidation Invalid(string error) => new(false, error);
}

/// <summary>One option of a list arranged in levels, as the browse endpoint returns it:
/// the option and whether options sit under it.</summary>
public sealed record OptionNode(CrestOptionModel Option, bool HasChildren);
