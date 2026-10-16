#nullable enable

using System.Text.Json;
using Grove.Contents.Management.Metadata;
using Grove.Contents.Management.Metadata.Models;

namespace Grove.Contents.Types;

/// <summary>A tenant asked for a definition change its lock forbids. Answered as 409 by <see cref="DefinitionLockedExceptionMapper"/>.</summary>
// Not an InvalidOperationException: the definition controllers turn those into 400s, and a
// refused lock must reach the filter as what it is.
public sealed class DefinitionLockedException(string message) : Exception(message);

/// <summary>
/// The one place that says whether a tenant-originated definition change is allowed under
/// the definition locks (<see cref="DefinitionLockSettings"/>). Called by Grove's
/// definition controllers before they alter, and by the decorator over the stock
/// content-types service, so the stock admin obeys the same locks. Module code never
/// calls it: migrations write through <see cref="IContentDefinitionManager"/> directly.
/// </summary>
public sealed class DefinitionLockGuard(IContentDefinitionManager contentDefinitionManager)
{
    /// <summary>Removing, retyping or changing the settings of a field: refused when the field or its part is locked.</summary>
    public async Task EnsureFieldChangeAsync(string fieldSetName, string fieldName, string change)
    {
        var part = await contentDefinitionManager.LoadFieldSetDefinitionAsync(fieldSetName);
        var field = part?.Fields.FirstOrDefault(candidate => string.Equals(candidate.Name, fieldName, StringComparison.OrdinalIgnoreCase));
        if (field is null)
        {
            return;
        }

        var lock_ = field.GetEffectiveLock(part);
        if (DefinitionLockSources.IsLocked(lock_))
        {
            throw new DefinitionLockedException($"Field '{fieldSetName}.{fieldName}' is locked ({lock_}): it cannot be {change}.");
        }
    }

    /// <summary>Deleting a part definition or changing its own settings: refused when the part is locked.</summary>
    public async Task EnsureFieldSetChangeAsync(string fieldSetName, string change)
    {
        var part = await contentDefinitionManager.LoadFieldSetDefinitionAsync(fieldSetName);
        var lock_ = part.GetLock();
        if (DefinitionLockSources.IsLocked(lock_))
        {
            throw new DefinitionLockedException($"Field set '{fieldSetName}' is locked ({lock_}): it cannot be {change}.");
        }
    }

    /// <summary>Detaching a part from a type or changing the attachment's non-display settings: refused when the attachment is locked.</summary>
    public async Task EnsureTypeFieldSetChangeAsync(string typeName, string fieldSetName, string change)
    {
        var type = await contentDefinitionManager.LoadTypeDefinitionAsync(typeName);
        var typeFieldSet = type?.FieldSets.FirstOrDefault(candidate => string.Equals(candidate.Name, fieldSetName, StringComparison.OrdinalIgnoreCase));
        var lock_ = typeFieldSet.GetLock();
        if (DefinitionLockSources.IsLocked(lock_))
        {
            throw new DefinitionLockedException($"Field set '{fieldSetName}' on type '{typeName}' is locked ({lock_}): it cannot be {change}.");
        }
    }

    /// <summary>
    /// Adding, changing or removing a type's field rule (<see cref="FieldRulesSettings"/>): refused
    /// when the field the rule applies to is locked, since the rule changes what that field accepts.
    /// </summary>
    public async Task EnsureFieldRulesChangeAsync(string typeName, IReadOnlyList<FieldRule> current, IReadOnlyList<FieldRule> requested)
    {
        static string Text(FieldRule rule) => JsonSerializer.Serialize(rule);

        var before = current.Select(Text).ToHashSet(StringComparer.Ordinal);
        var after = requested.Select(Text).ToHashSet(StringComparer.Ordinal);
        var changed = current.Where(rule => !after.Contains(Text(rule)))
            .Concat(requested.Where(rule => !before.Contains(Text(rule))))
            .Select(rule => rule.Field)
            .Distinct(StringComparer.OrdinalIgnoreCase);

        var type = await contentDefinitionManager.LoadTypeDefinitionAsync(typeName);
        foreach (var path in changed)
        {
            var segments = path?.Split('.') ?? [];
            var typeFieldSet = segments.Length == 2
                ? type?.FieldSets.FirstOrDefault(candidate => string.Equals(candidate.Name, segments[0], StringComparison.OrdinalIgnoreCase))
                : null;
            if (typeFieldSet?.FieldSetDefinition is { } fieldSet)
            {
                await EnsureFieldChangeAsync(fieldSet.Name, segments[1], DefinitionChanges.RuleChanged);
            }
        }
    }

    /// <summary>Removing an embedding or changing more than its display surface (display name, description, position): refused when the embedding is locked.</summary>
    public async Task EnsureEmbeddingChangeAsync(string typeName, string embeddingName, string change)
    {
        var type = await contentDefinitionManager.LoadTypeDefinitionAsync(typeName);
        var embedding = type?.Embeddings.FirstOrDefault(candidate => string.Equals(candidate.Name, embeddingName, StringComparison.OrdinalIgnoreCase));
        var lock_ = embedding.GetLock();
        if (DefinitionLockSources.IsLocked(lock_))
        {
            throw new DefinitionLockedException($"Embedding '{embeddingName}' on type '{typeName}' is locked ({lock_}): it cannot be {change}.");
        }
    }

    /// <summary>Deleting a type: refused while any of its attachments or embeddings is locked.</summary>
    public async Task EnsureTypeDeleteAsync(string typeName)
    {
        var type = await contentDefinitionManager.LoadTypeDefinitionAsync(typeName);
        var locked = type?.FieldSets.FirstOrDefault(part => DefinitionLockSources.IsLocked(part.GetLock()));
        if (locked is not null)
        {
            throw new DefinitionLockedException($"Type '{typeName}' carries the locked part '{locked.Name}' ({locked.GetLock()}): it cannot be deleted.");
        }

        var lockedEmbedding = type?.Embeddings.FirstOrDefault(embedding => DefinitionLockSources.IsLocked(embedding.GetLock()));
        if (lockedEmbedding is not null)
        {
            throw new DefinitionLockedException($"Type '{typeName}' carries the locked embedding '{lockedEmbedding.Name}' ({lockedEmbedding.GetLock()}): it cannot be deleted.");
        }
    }

    /// <summary>
    /// Placing or lifting a lock from the tenant side: never onto or off a Module lock. The
    /// permission (<see cref="ContentDefinitionPermissions.LockContentDefinitions"/>) is
    /// the controller's 403, not this 409.
    /// </summary>
    public static void EnsureLockChange(string current, string requested, string subject)
    {
        if (!DefinitionLockRules.CanTenantChange(current, requested))
        {
            throw new DefinitionLockedException($"{subject} has a module lock: it is the owning module's contract and cannot be placed or lifted in the tenant.");
        }
    }
}

/// <summary>The changes the guard names in its refusals.</summary>
public static class DefinitionChanges
{
    public const string Removed = "removed";
    public const string Retyped = "changed to another field type";
    public const string SettingsChanged = "changed (its settings are frozen; only display name, description and position may change)";
    public const string Deleted = "deleted";
    public const string Detached = "detached";
    public const string RuleChanged = "given, changed or relieved of a field rule";
}
