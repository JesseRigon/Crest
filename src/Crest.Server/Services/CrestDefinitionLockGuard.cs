using Crest.Permissions;
using Crest.Settings;
using Crest.ContentManagement.Metadata;
using Crest.ContentManagement.Metadata.Models;

namespace Crest.Services;

/// <summary>A tenant asked for a definition change its lock forbids. Answered as 409 by <see cref="Crest.Filters.CrestDefinitionLockExceptionFilter"/>.</summary>
// Not an InvalidOperationException: the definition controllers turn those into 400s, and a
// refused lock must reach the filter as what it is.
public sealed class CrestDefinitionLockedException(string message) : Exception(message);

/// <summary>
/// The one place that says whether a tenant-originated definition change is allowed under
/// the definition locks (<see cref="CrestDefinitionLockSettings"/>). Called by Crest's
/// definition controllers before they alter, and by the decorator over the stock
/// content-types service, so the stock admin obeys the same locks. Module code never
/// calls it: migrations write through <see cref="IContentDefinitionManager"/> directly.
/// </summary>
public sealed class CrestDefinitionLockGuard(IContentDefinitionManager contentDefinitionManager)
{
    /// <summary>Removing, retyping or changing the settings of a field: refused when the field or its part is locked.</summary>
    public async Task EnsureFieldChangeAsync(string partName, string fieldName, string change)
    {
        var part = await contentDefinitionManager.LoadPartDefinitionAsync(partName);
        var field = part?.Fields.FirstOrDefault(candidate => string.Equals(candidate.Name, fieldName, StringComparison.OrdinalIgnoreCase));
        if (field is null)
        {
            return;
        }

        var lock_ = field.GetEffectiveLock(part);
        if (CrestDefinitionLockSources.IsLocked(lock_))
        {
            throw new CrestDefinitionLockedException($"Field '{partName}.{fieldName}' is locked ({lock_}): it cannot be {change}.");
        }
    }

    /// <summary>Deleting a part definition or changing its own settings: refused when the part is locked.</summary>
    public async Task EnsurePartChangeAsync(string partName, string change)
    {
        var part = await contentDefinitionManager.LoadPartDefinitionAsync(partName);
        var lock_ = part.GetLock();
        if (CrestDefinitionLockSources.IsLocked(lock_))
        {
            throw new CrestDefinitionLockedException($"Part '{partName}' is locked ({lock_}): it cannot be {change}.");
        }
    }

    /// <summary>Detaching a part from a type or changing the attachment's non-display settings: refused when the attachment is locked.</summary>
    public async Task EnsureTypePartChangeAsync(string typeName, string partName, string change)
    {
        var type = await contentDefinitionManager.LoadTypeDefinitionAsync(typeName);
        var typePart = type?.Parts.FirstOrDefault(candidate => string.Equals(candidate.Name, partName, StringComparison.OrdinalIgnoreCase));
        var lock_ = typePart.GetLock();
        if (CrestDefinitionLockSources.IsLocked(lock_))
        {
            throw new CrestDefinitionLockedException($"Part '{partName}' on type '{typeName}' is locked ({lock_}): it cannot be {change}.");
        }
    }

    /// <summary>Deleting a type: refused while any of its attachments is locked.</summary>
    public async Task EnsureTypeDeleteAsync(string typeName)
    {
        var type = await contentDefinitionManager.LoadTypeDefinitionAsync(typeName);
        var locked = type?.Parts.FirstOrDefault(part => CrestDefinitionLockSources.IsLocked(part.GetLock()));
        if (locked is not null)
        {
            throw new CrestDefinitionLockedException($"Type '{typeName}' carries the locked part '{locked.Name}' ({locked.GetLock()}): it cannot be deleted.");
        }
    }

    /// <summary>
    /// Placing or lifting a lock from the tenant side: never onto or off a Module lock. The
    /// permission (<see cref="CrestContentDefinitionPermissions.LockContentDefinitions"/>) is
    /// the controller's 403, not this 409.
    /// </summary>
    public static void EnsureLockChange(string current, string requested, string subject)
    {
        if (!CrestDefinitionLockRules.CanTenantChange(current, requested))
        {
            throw new CrestDefinitionLockedException($"{subject} has a module lock: it is the owning module's contract and cannot be placed or lifted in the tenant.");
        }
    }
}

/// <summary>The changes the guard names in its refusals.</summary>
public static class CrestDefinitionChanges
{
    public const string Removed = "removed";
    public const string Retyped = "changed to another field type";
    public const string SettingsChanged = "changed (its settings are frozen; only display name, description and position may change)";
    public const string Deleted = "deleted";
    public const string Detached = "detached";
}
