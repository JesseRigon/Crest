using Grove.Models;

namespace Grove.Services;

/// <summary>
/// Grove's tenant-editable enum system. Modules DECLARE the sets they own and consume
/// them by logical key; tenants add, relabel, reorder and hide options at runtime.
/// </summary>
public interface IContentPartListService
{
    /// <summary>Every set in the tenant.</summary>
    Task<IReadOnlyList<GroveContentPartListModel>> ListAsync(CancellationToken cancellationToken = default);

    /// <summary>Resolves a set by its stable logical key (never by ContentItemId,
    /// which differs per tenant). Null when the set does not exist.</summary>
    Task<GroveContentPartListModel?> GetAsync(string key, CancellationToken cancellationToken = default);

    /// <summary>The options an editor should offer for a set: ordered, hidden ones
    /// dropped. Empty when the set does not exist.</summary>
    Task<IReadOnlyList<GroveOptionModel>> GetSelectableAsync(string key, CancellationToken cancellationToken = default);

    /// <summary>Translates a stored ContentItemId back to the option's technical key
    /// - the lookup content-driven code needs, since content stores ids while code
    /// compares keys. Null when the id is not an option of that set.</summary>
    Task<string?> ResolveKeyAsync(string listKey, string optionContentItemId, CancellationToken cancellationToken = default);

    /// <summary>Translates a technical key to the ContentItemId to store in a
    /// TaxonomyField. Null when the set has no such option.</summary>
    Task<string?> ResolveContentItemIdAsync(string listKey, string optionKey, CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates a set if absent and adds any options it does not yet have, marking
    /// both as <see cref="OptionSources.Module"/>. Idempotent, and it NEVER
    /// overwrites an existing option: a tenant's relabel or hide survives reseeding.
    /// This is how a module declares the lists it owns (call from a migration or a
    /// feature-enable path).
    /// </summary>
    Task<GroveContentPartListModel> SeedAsync(ContentPartListSeed seed, CancellationToken cancellationToken = default);

    /// <summary>Creates a tenant-owned set. Fails when the key is invalid or taken.</summary>
    Task<GroveContentPartListModel> CreateListAsync(string key, string displayText, CancellationToken cancellationToken = default);

    /// <summary>Adds a tenant-owned option to a set. Fails when the key is invalid or
    /// already used in that set. A null or blank category files the option under
    /// Uncategorized; a null or blank plural label means the singular serves both;
    /// a null or blank machine value means the key is the option's only datum.
    /// <paramref name="fields"/> sets custom data field values by field name -
    /// unknown field names fail.</summary>
    /// <paramref name="parent"/> places the option under another option of the same list
    /// (a list arranged in levels); null or blank places it at the top.
    Task<GroveOptionModel> AddOptionAsync(string listKey, string optionKey, string displayText, int position = 0, string? category = null, string? displayTextPlural = null, string? value = null, IReadOnlyDictionary<string, string?>? fields = null, string? parent = null, CancellationToken cancellationToken = default);

    /// <summary>Relabels, repositions, recategorizes, revalues, or hides/unhides an
    /// option. Null leaves a value unchanged (a BLANK plural label or machine value
    /// clears it). <paramref name="fields"/> updates custom data field values by
    /// field name with the same per-entry contract: a null dictionary touches
    /// nothing, a blank entry clears that field, an unknown field name fails.
    /// The technical key is deliberately NOT editable
    /// here - it is the module's contract with code.</summary>
    /// <paramref name="parent"/>: null leaves the option where it is, blank moves it to the
    /// top, a key moves it under that option (refused when it would make a cycle).
    Task<GroveOptionModel> UpdateOptionAsync(string listKey, string optionKey, string? displayText, int? position, bool? hidden, string? category = null, string? displayTextPlural = null, string? value = null, IReadOnlyDictionary<string, string?>? fields = null, string? parent = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Sets the per-field lock designation for a custom data field on the list's
    /// option content type (see <see cref="Grove.Settings.GroveOptionFieldSettings"/>):
    /// true makes the field machine surface (frozen under the list's data lock),
    /// false makes it display surface. Fails when the option type has no such field.
    /// NOTE: like every service method, this trusts its caller - the controller
    /// enforces who may change designations and when.
    /// </summary>
    Task<GroveContentPartListModel> SetOptionFieldLockAsync(string listKey, string fieldName, bool dataLocked, CancellationToken cancellationToken = default);

    /// <summary>The content types eligible to serve as a list's option type: every
    /// type carrying GroveOptionPart. The shared "Option" type is always among them.</summary>
    Task<IReadOnlyList<string>> GetOptionContentTypesAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Points the list at a dedicated option content type (per-list custom data
    /// fields live on it). Fails when the type does not exist or lacks
    /// GroveOptionPart, and when the list already holds options of another type -
    /// existing option items keep their stored type, so retargeting a populated list
    /// would orphan their data. Like every service method this trusts its caller;
    /// the controller enforces permissions and the lock rules.
    /// </summary>
    Task<GroveContentPartListModel> SetOptionContentTypeAsync(string listKey, string contentType, CancellationToken cancellationToken = default);

    /// <summary>
    /// Rewrites the list's MANUAL order: each option's Position becomes its index in
    /// <paramref name="orderedKeys"/>, which must name every option exactly once.
    /// This is the only list-owned ordering - how an instance SORTS the list for
    /// display is the consuming picker's SortColumns, not list data.
    /// </summary>
    Task<GroveContentPartListModel> ReorderOptionsAsync(string key, IReadOnlyList<string> orderedKeys, CancellationToken cancellationToken = default);

    /// <summary>
    /// Places or lifts TENANT-authority locks (null leaves a lock unchanged). Fails
    /// when the named lock was placed by the owning module - the module contract is
    /// out of the tenant's reach in both directions.
    /// NOTE: lock ENFORCEMENT lives at the tenant API surface (the controller);
    /// service methods trust their callers, because module code and migrations
    /// legitimately edit what tenants must not.
    /// </summary>
    Task<GroveContentPartListModel> UpdateListLocksAsync(string key, bool? dataLock, bool? editLock, CancellationToken cancellationToken = default);

    /// <summary>Deletes a tenant-owned set and the options contained in it. Fails for
    /// module-seeded sets - their keys are a module's contract, so they can only be
    /// hidden, never removed.</summary>
    Task DeleteListAsync(string key, CancellationToken cancellationToken = default);

    /// <summary>
    /// Attaches a set to a content type as an OptionPickerField, so any type can
    /// reference an Content part list at runtime. <paramref name="configure"/> lets the
    /// caller shape the attachment's picker settings (filters for cascades, sort
    /// columns, ...) after the defaults are applied; it runs on every (re)attach, so
    /// migrations declare the settings idempotently.
    /// </summary>
    Task AttachToContentTypeAsync(string listKey, string contentType, string fieldName, string? displayName = null, Action<Grove.Settings.OptionPickerFieldSettings>? configure = null, CancellationToken cancellationToken = default);
}
