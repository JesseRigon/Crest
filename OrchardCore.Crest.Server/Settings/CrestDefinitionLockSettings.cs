using OrchardCore.ContentManagement.Metadata.Builders;
using OrchardCore.ContentManagement.Metadata.Models;

namespace Crest.Settings;

/// <summary>
/// A lock on a content DEFINITION: a part definition, one of its fields, or a part's
/// attachment to a type (docs/content-items.md › Definition locks). The definition-level
/// counterpart of the option-list locks - one mechanism, two authorities
/// (<see cref="CrestDefinitionLockSources"/>): a Module lock is the owning module's
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
/// editable. Enforced for tenant-originated writes only - Crest's definition controllers
/// and the stock content-types service - never for module code (migrations re-assert
/// what tenants must not change).
/// </remarks>
public class CrestDefinitionLockSettings
{
    /// <summary>One of <see cref="CrestDefinitionLockSources"/>; None when absent.</summary>
    public string Lock { get; set; } = CrestDefinitionLockSources.None;
}

/// <summary>Who placed a definition lock, which decides who may lift it.</summary>
public static class CrestDefinitionLockSources
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
public static class CrestDefinitionLockExtensions
{
    public static string GetLock(this ContentPartDefinition? part) =>
        CrestDefinitionLockSources.Normalize(part?.GetSettings<CrestDefinitionLockSettings>()?.Lock);

    public static string GetLock(this ContentPartFieldDefinition? field) =>
        CrestDefinitionLockSources.Normalize(field?.GetSettings<CrestDefinitionLockSettings>()?.Lock);

    public static string GetLock(this ContentTypePartDefinition? typePart) =>
        CrestDefinitionLockSources.Normalize(typePart?.GetSettings<CrestDefinitionLockSettings>()?.Lock);

    /// <summary>A field is locked by its own lock or by its part's; the stronger source wins.</summary>
    public static string GetEffectiveLock(this ContentPartFieldDefinition? field, ContentPartDefinition? part) =>
        CrestDefinitionLockRules.Strongest(field.GetLock(), part.GetLock());

    public static ContentPartFieldDefinitionBuilder Locked(this ContentPartFieldDefinitionBuilder builder, string source = CrestDefinitionLockSources.Module) =>
        builder.MergeSettings<CrestDefinitionLockSettings>(settings => settings.Lock = CrestDefinitionLockSources.Normalize(source));

    public static ContentPartDefinitionBuilder Locked(this ContentPartDefinitionBuilder builder, string source = CrestDefinitionLockSources.Module) =>
        builder.MergeSettings<CrestDefinitionLockSettings>(settings => settings.Lock = CrestDefinitionLockSources.Normalize(source));

    public static ContentTypePartDefinitionBuilder Locked(this ContentTypePartDefinitionBuilder builder, string source = CrestDefinitionLockSources.Module) =>
        builder.MergeSettings<CrestDefinitionLockSettings>(settings => settings.Lock = CrestDefinitionLockSources.Normalize(source));
}

/// <summary>The pure rules: which source outranks which, and who may move a lock where.</summary>
public static class CrestDefinitionLockRules
{
    private static int Rank(string source) => CrestDefinitionLockSources.Normalize(source) switch
    {
        CrestDefinitionLockSources.Module => 2,
        CrestDefinitionLockSources.Tenant => 1,
        _ => 0,
    };

    public static string Strongest(string first, string second) => Rank(first) >= Rank(second) ? CrestDefinitionLockSources.Normalize(first) : CrestDefinitionLockSources.Normalize(second);

    /// <summary>
    /// A tenant may move a lock between None and Tenant (with the permission, checked by the
    /// caller); a Module lock never moves in-tenant, and no tenant may place one.
    /// </summary>
    public static bool CanTenantChange(string current, string requested)
    {
        var from = CrestDefinitionLockSources.Normalize(current);
        var to = CrestDefinitionLockSources.Normalize(requested);
        return from != CrestDefinitionLockSources.Module && to != CrestDefinitionLockSources.Module;
    }

    /// <summary>The settings sections a lock leaves editable: the display surface.</summary>
    public static readonly IReadOnlySet<string> FieldDisplaySections = new HashSet<string>(StringComparer.Ordinal) { "ContentPartFieldSettings" };
    public static readonly IReadOnlySet<string> TypePartDisplaySections = new HashSet<string>(StringComparer.Ordinal) { "ContentTypePartSettings" };
    public static readonly IReadOnlySet<string> PartDisplaySections = new HashSet<string>(StringComparer.Ordinal) { "ContentPartSettings" };
}
