using Crest.Data.Migration;
using YesSql;

namespace Crest.Data.Global;

/// <summary>
/// The tenant-less YesSql store. Every use is a unit of work: outside a shell scope nothing
/// auto-commits, so <see cref="WriteAsync"/> saves and the store never hands out a raw
/// session. Reads and writes from any tenant's code go through this; only the loader and
/// the Default tenant hold write paths (docs/global-store.md).
/// </summary>
public interface IGlobalStore
{
    Task<T> ReadAsync<T>(Func<ISession, CancellationToken, Task<T>> query, CancellationToken cancellationToken = default);

    /// <summary>A write is a unit of work and bumps the store's data version, which invalidates <see cref="IGlobalCache"/> everywhere.</summary>
    Task WriteAsync(Func<ISession, CancellationToken, Task> work, CancellationToken cancellationToken = default);

    /// <summary>The store's data version: incremented by every write, by any host process. Cheap to read.</summary>
    Task<long> DataVersionAsync(CancellationToken cancellationToken = default);

    /// <summary>Builds the store and runs every global migration. Idempotent; called lazily by the first read or write.</summary>
    Task EnsureInitializedAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// A migration of the global store: index tables and their loaders, on the platform's own
/// <see cref="DataMigration"/> convention (<c>CreateAsync</c>, then <c>UpdateFromNAsync</c>),
/// with the applied versions recorded in the global store's own
/// <see cref="Crest.Data.Migration.Records.DataMigrationRecord"/>. Discovered by scanning module
/// assemblies, so a module declares its global documents without the host naming it, and run
/// once, on the store's first use - never by a tenant's migration manager.
/// </summary>
/// <remarks>
/// The store outlives every tenant container, so a global migration takes no services: it
/// has a public parameterless constructor and works through <see cref="DataMigration.SchemaBuilder"/>
/// and <see cref="Session"/>, both set before each step. A step that reloads shipped data
/// upserts, so it is safe on a populated store.
/// </remarks>
public abstract class GlobalDataMigration : DataMigration
{
    /// <summary>The global store's session for this run; saved when every migration has run.</summary>
    public ISession Session { get; set; } = default!;

    /// <summary>Index providers this migration's documents need registered on the store.</summary>
    public virtual IEnumerable<global::YesSql.Indexes.IIndexProvider> IndexProviders() => [];

    /// <summary>
    /// The <c>IGlobalSchema</c> name this migration replaced, so a store built before
    /// global schemas moved onto <see cref="DataMigration"/> keeps its applied version (its
    /// final version equals that schema's). Transitional: drop once no such store remains.
    /// </summary>
    public virtual string? LegacySchemaName => null;
}

/// <summary>The store's data version: one row, bumped by every write.</summary>
public sealed class CrestGlobalSchemaState
{
    public long Id { get; set; }

    /// <summary>
    /// Applied versions from before global schemas moved onto <see cref="DataMigration"/>;
    /// read once to seed the <see cref="Crest.Data.Migration.Records.DataMigrationRecord"/>, then cleared.
    /// </summary>
    public Dictionary<string, int> Versions { get; set; } = new(StringComparer.Ordinal);
    /// <summary>Bumped by every write; the cache key every host process checks against.</summary>
    public long DataVersion { get; set; }
}

/// <summary>
/// Host-level cache over global reads, version-keyed (docs/global-store.md phase 7). An entry
/// is valid while the store's data version is the one it was read at; a write anywhere -
/// this process, another host on the same database - moves the version and every entry
/// lapses. The version is re-read from the store at most once per <see cref="GlobalStoreOptions.CacheVersionCheckSeconds"/>,
/// so a hot read costs nothing and a stale read is bounded by that window.
/// </summary>
public interface IGlobalCache
{
    Task<T> GetOrCreateAsync<T>(string key, Func<CancellationToken, Task<T>> factory, CancellationToken cancellationToken = default);

    /// <summary>Drops every entry now, without waiting for the version check.</summary>
    void Clear();
}
