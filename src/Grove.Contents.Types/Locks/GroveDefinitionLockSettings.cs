#nullable enable

using Grove.Contents.Management.Metadata.Builders;
using Grove.Contents.Management.Metadata.Models;

namespace Grove.Contents.Types;

/// <summary>
/// A lock on a content DEFINITION: a part definition, one of its fields, or a part's
/// attachment to a type (docs/content-items.md › Definition locks). The definition-level
/// counterpart of the option-list locks - one mechanism, two authorities
/// (<see cref="DefinitionLockSources"/>): a Module lock is the owning module's
/// contract (a system process downstream depends on the field being there, of that type,
/// with those settings) and no one in the tenant can lift it; a Tenant lock is placed and
/// lifted by whoever holds <c>LockContentDefinitions</c>.
/// </summary>
/// <remarks>
/// What a lock freezes is existence and SHAPE, never values: a locked field cannot be
/// removed, retyped (a conversion to or from a picker is a retype), pointed at another
/// picker source, made optional or hidden by a condition; every settings section other than
/// its display surface (<c>ContentPartFieldSettings</c>: display name, description,
/// position, editor, display mode) is frozen. A locked picker's list is still edited as
/// ever, and item data is untouched. A locked
/// part definition cannot be deleted and every field it has is locked as if individually
/// (tenant fields may still be ADDED to it). A locked attachment cannot be detached and its
/// type cannot be deleted; its display surface (<c>ContentTypePartSettings</c>) stays
/// editable. Enforced for tenant-originated writes only - Grove's definition controllers
/// and the stock content-types service - never for module code (migrations re-assert
/// what tenants must not change).
/// </remarks>
public class GroveDefinitionLockSettings
{
    /// <summary>One of <see cref="DefinitionLockSources"/>; None when absent.</summary>
    public string Lock { get; set; } = DefinitionLockSources.None;
}

/// <summary>Who placed a definition lock, which decides who may lift it.</summary>
public static class DefinitionLockSources
{
    public const string None = "None";

    /// <summary>Placed by the tenant (the LockContentDefinitions permission); the same permission lifts it.</summary>
    public const string Tenant = "Tenant";

    /// <summary>Declared by the owning module's migration; immutable within the tenant.</summary>
    public const string Module = "Module";

    public static readonly IReadOnlyList<string> All = [None, Tenant, Module];

    public static string Normalize(string? value) =>
        All.FirstOrDefault(candidate => string.Equals(candidate, value?.Trim(), StringComparison.OrdinalIgnoreCase)) ?? None;

    public static bool IsLocked(string? value) => Normalize(value) != None;
}

/// <summary>
/// Reads and writes locks on definitions and their builders. Migrations call
/// <c>Locked()</c> on a field, part or attachment builder to declare the module's contract.
/// </summary>
public static class DefinitionLockExtensions
{
    public static string GetLock(this ContentPartDefinition? part) =>
        DefinitionLockSources.Normalize(part?.GetSettings<GroveDefinitionLockSettings>()?.Lock);

    public static string GetLock(this ContentPartFieldDefinition? field) =>
        DefinitionLockSources.Normalize(field?.GetSettings<GroveDefinitionLockSettings>()?.Lock);

    public static string GetLock(this ContentTypePartDefinition? typePart) =>
        DefinitionLockSources.Normalize(typePart?.GetSettings<GroveDefinitionLockSettings>()?.Lock);

    /// <summary>A field is locked by its own lock or by its part's; the stronger source wins.</summary>
    public static string GetEffectiveLock(this ContentPartFieldDefinition? field, ContentPartDefinition? part) =>
        DefinitionLockRules.Strongest(field.GetLock(), part.GetLock());

    public static ContentPartFieldDefinitionBuilder Locked(this ContentPartFieldDefinitionBuilder builder, string source = DefinitionLockSources.Module) =>
        builder.MergeSettings<GroveDefinitionLockSettings>(settings => settings.Lock = DefinitionLockSources.Normalize(source));

    public static ContentPartDefinitionBuilder Locked(this ContentPartDefinitionBuilder builder, string source = DefinitionLockSources.Module) =>
        builder.MergeSettings<GroveDefinitionLockSettings>(settings => settings.Lock = DefinitionLockSources.Normalize(source));

    public static ContentTypePartDefinitionBuilder Locked(this ContentTypePartDefinitionBuilder builder, string source = DefinitionLockSources.Module) =>
        builder.MergeSettings<GroveDefinitionLockSettings>(settings => settings.Lock = DefinitionLockSources.Normalize(source));
}

/// <summary>The pure rules: which source outranks which, and who may move a lock where.</summary>
public static class DefinitionLockRules
{
    private static int Rank(string source) => DefinitionLockSources.Normalize(source) switch
    {
        DefinitionLockSources.Module => 2,
        DefinitionLockSources.Tenant => 1,
        _ => 0,
    };

    public static string Strongest(string first, string second) => Rank(first) >= Rank(second) ? DefinitionLockSources.Normalize(first) : DefinitionLockSources.Normalize(second);

    /// <summary>
    /// A tenant may move a lock between None and Tenant (with the permission, checked by the
    /// caller); a Module lock never moves in-tenant, and no tenant may place one.
    /// </summary>
    public static bool CanTenantChange(string current, string requested)
    {
        var from = DefinitionLockSources.Normalize(current);
        var to = DefinitionLockSources.Normalize(requested);
        return from != DefinitionLockSources.Module && to != DefinitionLockSources.Module;
    }

    /// <summary>The settings sections a lock leaves editable: the display surface.</summary>
    public static readonly IReadOnlySet<string> FieldDisplaySections = new HashSet<string>(StringComparer.Ordinal) { "ContentPartFieldSettings" };
    public static readonly IReadOnlySet<string> TypePartDisplaySections = new HashSet<string>(StringComparer.Ordinal) { "ContentTypePartSettings" };
    public static readonly IReadOnlySet<string> PartDisplaySections = new HashSet<string>(StringComparer.Ordinal) { "ContentPartSettings" };
}
