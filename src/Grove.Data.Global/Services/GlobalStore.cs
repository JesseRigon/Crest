using System.Reflection;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Grove.Data;
using Grove.Data.Migration;
using Grove.Data.Migration.Records;
using Grove.Environment.Shell;
using Grove.Json;
using YesSql;
using YesSql.Indexes;
using YesSql.Provider.MySql;
using YesSql.Provider.PostgreSql;
using YesSql.Provider.Sqlite;
using YesSql.Provider.SqlServer;
using YesSql.Serialization;
using YesSql.Sql;

namespace Grove.Data.Global.Services;

/// <summary>
/// Process-wide singleton (see <see cref="GlobalHost"/>) owning the tenant-less store. Built the way Grove.Data
/// builds a tenant's store (a hand-assembled YesSql <see cref="Configuration"/> switched
/// on the provider) but from <see cref="GlobalStoreOptions"/> instead of ShellSettings,
/// so no shell is involved. Schema steps are discovered from every loaded module assembly
/// deriving from <see cref="GlobalDataMigration"/> and run once, versioned, on first use.
/// </summary>
// NOT IDisposable/IAsyncDisposable, deliberately. Grove clones host singletons into
// each tenant container through a factory delegate, and MS DI disposes factory-produced
// disposables when a tenant container is torn down (feature toggle, shell reload) - which
// would dispose this process-wide store under every other tenant. The underlying IStore is
// released on application shutdown by GlobalHost instead.
public sealed class GlobalStore : IGlobalStore
{
    public const string GlobalFolderName = "GroveGlobal";

    private readonly Microsoft.Extensions.Configuration.IConfiguration _configuration;
    private readonly IOptions<ShellOptions> _shellOptions;
    private readonly IOptions<DocumentJsonSerializerOptions> _serializerOptions;
    private readonly ILoggerFactory _loggerFactory;
    private readonly ILogger<GlobalStore> _logger;
    private readonly SemaphoreSlim _initLock = new(1, 1);
    private IStore? _store;
    private IReadOnlyList<GlobalDataMigration> _migrations = [];

    public GlobalStore(
        Microsoft.Extensions.Configuration.IConfiguration configuration,
        IOptions<ShellOptions> shellOptions,
        IOptions<DocumentJsonSerializerOptions> serializerOptions,
        ILoggerFactory loggerFactory)
    {
        _configuration = configuration;
        _shellOptions = shellOptions;
        _serializerOptions = serializerOptions;
        _loggerFactory = loggerFactory;
        _logger = loggerFactory.CreateLogger<GlobalStore>();
    }

    public async Task<T> ReadAsync<T>(Func<ISession, CancellationToken, Task<T>> query, CancellationToken cancellationToken = default)
    {
        var store = await GetStoreAsync(cancellationToken);
        await using var session = store.CreateSession();
        return await query(session, cancellationToken);
    }

    public async Task WriteAsync(Func<ISession, CancellationToken, Task> work, CancellationToken cancellationToken = default)
    {
        var store = await GetStoreAsync(cancellationToken);
        await using var session = store.CreateSession();
        try
        {
            await work(session, cancellationToken);
            var state = await session.Query<GroveGlobalSchemaState>().FirstOrDefaultAsync(cancellationToken) ?? new GroveGlobalSchemaState();
            state.DataVersion++;
            await session.SaveAsync(state);
            await session.SaveChangesAsync();
        }
        catch
        {
            await session.CancelAsync();
            throw;
        }
    }

    public async Task<long> DataVersionAsync(CancellationToken cancellationToken = default)
    {
        var store = await GetStoreAsync(cancellationToken);
        await using var session = store.CreateSession();
        return (await session.Query<GroveGlobalSchemaState>().FirstOrDefaultAsync(cancellationToken))?.DataVersion ?? 0;
    }

    public Task EnsureInitializedAsync(CancellationToken cancellationToken = default) => GetStoreAsync(cancellationToken);

    private async Task<IStore> GetStoreAsync(CancellationToken cancellationToken)
    {
        if (_store is not null)
        {
            return _store;
        }

        await _initLock.WaitAsync(cancellationToken);
        try
        {
            if (_store is not null)
            {
                return _store;
            }

            var options = ReadOptions();
            var configuration = BuildConfiguration(options);
            _migrations = DiscoverMigrations();

            var store = await StoreFactory.CreateAndInitializeAsync(configuration);
            store.RegisterIndexes(_migrations.SelectMany(migration => migration.IndexProviders()).ToArray());

            await MigrateAsync(store, cancellationToken);

            _store = store;
            _logger.LogInformation("Grove global store ready ({Provider}, prefix '{Prefix}', {Migrations} migration(s)).",
                options.DatabaseProvider ?? DatabaseProviderValue.Sqlite, options.TablePrefix, _migrations.Count);
            return store;
        }
        finally
        {
            _initLock.Release();
        }
    }

    private GlobalStoreOptions ReadOptions()
    {
        var options = Microsoft.Extensions.Configuration.ConfigurationBinder.Get<GlobalStoreOptions>(
            _configuration.GetSection("Grove").GetSection(GlobalStoreOptions.SectionName)) ?? new GlobalStoreOptions();

        options.TablePrefix ??= GlobalStoreOptions.DefaultTablePrefix;
        return options;
    }

    private Configuration BuildConfiguration(GlobalStoreOptions options)
    {
        var tableOptions = new DatabaseTableOptions
        {
            DocumentTable = "Document",
            TableNameSeparator = "_",
            IdentityColumnSize = nameof(IdentityColumnSize.Int64),
        };

        var configuration = new Configuration
        {
            TableNameConvention = new TableNameConventionFactory().Create(tableOptions),
            IdentityColumnSize = IdentityColumnSize.Int64,
            Logger = _loggerFactory.CreateLogger("global::YesSql.GroveGlobal"),
            ContentSerializer = new DefaultContentJsonSerializer(_serializerOptions.Value.SerializerOptions),
        };

        var provider = string.IsNullOrWhiteSpace(options.DatabaseProvider) ? DatabaseProviderValue.Sqlite : options.DatabaseProvider;
        switch (provider)
        {
            case DatabaseProviderValue.Sqlite:
                var folder = Path.Combine(_shellOptions.Value.ShellsApplicationDataPath, GlobalFolderName);
                Directory.CreateDirectory(folder);
                var connectionString = SqliteHelper.GetConnectionString(
                    new SqliteOptions(), folder,
                    string.IsNullOrWhiteSpace(options.DatabaseName) ? GlobalStoreOptions.DefaultSqliteDatabaseName : options.DatabaseName,
                    SqliteOpenMode.ReadWriteCreate);
                configuration.UseSqLite(connectionString).UseDefaultIdGenerator();
                break;
            case DatabaseProviderValue.Postgres:
                configuration.UsePostgreSql(Require(options.ConnectionString, provider), schema: options.Schema).UseBlockIdGenerator();
                break;
            case DatabaseProviderValue.SqlConnection:
                configuration.UseSqlServer(Require(options.ConnectionString, provider), schema: options.Schema).UseBlockIdGenerator();
                break;
            case DatabaseProviderValue.MySql:
                configuration.UseMySql(Require(options.ConnectionString, provider), schema: options.Schema).UseBlockIdGenerator();
                break;
            default:
                throw new InvalidOperationException($"Unknown database provider '{provider}' for the Grove global store.");
        }

        if (!string.IsNullOrWhiteSpace(options.TablePrefix))
        {
            configuration.SetTablePrefix(options.TablePrefix.Trim() + tableOptions.TableNameSeparator);
        }

        return configuration;
    }

    private static string Require(string? connectionString, string provider) =>
        string.IsNullOrWhiteSpace(connectionString)
            ? throw new InvalidOperationException($"The Grove global store needs a ConnectionString for provider '{provider}' (section Grove:{GlobalStoreOptions.SectionName}).")
            : connectionString;

    // Modules contribute global documents without the host naming each one: any loaded
    // assembly that references Grove.Data.Global is scanned for GlobalDataMigration types.
    // Grove loads every module assembly at startup, before any shell is built, so the
    // first store use sees them all.
    private static IReadOnlyList<GlobalDataMigration> DiscoverMigrations()
    {
        var self = typeof(GlobalDataMigration).Assembly.GetName().Name;
        var migrations = new List<GlobalDataMigration>();

        foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
        {
            if (assembly.IsDynamic)
            {
                continue;
            }

            var references = assembly == typeof(GlobalDataMigration).Assembly
                || assembly.GetReferencedAssemblies().Any(reference => string.Equals(reference.Name, self, StringComparison.Ordinal));
            if (!references)
            {
                continue;
            }

            Type[] types;
            try
            {
                types = assembly.GetTypes();
            }
            catch (ReflectionTypeLoadException ex)
            {
                types = ex.Types.Where(type => type is not null).Select(type => type!).ToArray();
            }

            foreach (var type in types)
            {
                if (type.IsAbstract || !typeof(GlobalDataMigration).IsAssignableFrom(type))
                {
                    continue;
                }

                // No services: the store outlives every tenant container (see GlobalDataMigration).
                if (Activator.CreateInstance(type) is GlobalDataMigration migration)
                {
                    migrations.Add(migration);
                }
            }
        }

        return migrations.OrderBy(migration => migration.GetType().FullName, StringComparer.Ordinal).ToArray();
    }

    // The platform's DataMigration convention (DataMigrationSteps), recorded in the global
    // store's own DataMigrationRecord the way a tenant records its migrations.
    private async Task MigrateAsync(IStore store, CancellationToken cancellationToken)
    {
        await using var session = store.CreateSession();
        try
        {
            var record = await session.Query<DataMigrationRecord>().FirstOrDefaultAsync(cancellationToken);
            if (record is null)
            {
                record = new DataMigrationRecord();
                await SeedFromLegacyVersionsAsync(session, record, cancellationToken);
            }

            foreach (var migration in _migrations)
            {
                var className = migration.GetType().FullName!;
                var entry = record.DataMigrations.FirstOrDefault(applied => applied.DataMigrationClass == className);
                var current = entry?.Version ?? 0;

                migration.SchemaBuilder = new SchemaBuilder(store.Configuration, await session.BeginTransactionAsync());
                migration.Session = session;

                var version = await DataMigrationSteps.RunAsync(migration, current, from =>
                    _logger.LogInformation("Applying Grove global migration '{Migration}' from version {Version}.", className, from));

                if (version != 0 && version != current)
                {
                    if (entry is null)
                    {
                        entry = new Grove.Data.Migration.Records.DataMigration { DataMigrationClass = className };
                        record.DataMigrations.Add(entry);
                    }

                    entry.Version = version;
                }
            }

            await session.SaveAsync(record);
            await session.SaveChangesAsync();
        }
        catch
        {
            await session.CancelAsync();
            throw;
        }
    }

    // A store built while global schemas versioned themselves (GroveGlobalSchemaState.Versions)
    // keeps each schema's applied version under the migration that replaced it.
    private async Task SeedFromLegacyVersionsAsync(ISession session, DataMigrationRecord record, CancellationToken cancellationToken)
    {
        var state = await session.Query<GroveGlobalSchemaState>().FirstOrDefaultAsync(cancellationToken);
        if (state is not { Versions.Count: > 0 })
        {
            return;
        }

        foreach (var migration in _migrations)
        {
            if (migration.LegacySchemaName is { } name && state.Versions.TryGetValue(name, out var version))
            {
                record.DataMigrations.Add(new Grove.Data.Migration.Records.DataMigration
                {
                    DataMigrationClass = migration.GetType().FullName,
                    Version = version,
                });
            }
        }

        state.Versions.Clear();
        await session.SaveAsync(state);
    }

    /// <summary>Releases the underlying store. Called once, at application shutdown - never by a tenant container.</summary>
    public void Shutdown()
    {
        _store?.Dispose();
        _store = null;
    }
}
