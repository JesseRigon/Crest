using System.Data;
using System.Data.Common;
using System.Text.Json.Nodes;
using Grove.Data.Connectors;
using Grove.Data.Protocols.Http;
using Microsoft.Data.SqlClient;
using Microsoft.Data.Sqlite;
using MySqlConnector;
using Npgsql;

namespace Grove.Data.Protocols.Sql;

/// <summary>The settings and request shapes of a SQL connection.</summary>
public static class SqlConnectionSettings
{
    /// <summary><see cref="SqlProviders"/>.</summary>
    public const string Provider = "sql:provider";
    public const string Host = "sql:host";
    public const string Port = "sql:port";
    public const string Database = "sql:database";

    /// <summary>Most rows a query returns (default 1000).</summary>
    public const string MaxRows = "sql:maxRows";

    /// <summary>A raw request's method: a query returning rows, or a command returning the rows affected.</summary>
    public const string QueryMethod = "QUERY";
    public const string ExecuteMethod = "EXECUTE";
}

public static class SqlProviders
{
    public const string SqlServer = "sqlserver";
    public const string PostgreSql = "postgresql";
    public const string MySql = "mysql";
    public const string Sqlite = "sqlite";

    public static readonly IReadOnlyList<string> All = [SqlServer, PostgreSql, MySql, Sqlite];

    public static DbProviderFactory? Factory(string? provider) => provider?.ToLowerInvariant() switch
    {
        SqlServer => SqlClientFactory.Instance,
        PostgreSql => NpgsqlFactory.Instance,
        MySql => MySqlConnectorFactory.Instance,
        Sqlite => SqliteFactory.Instance,
        _ => null,
    };
}

/// <summary>
/// An external database (SQL Server, PostgreSQL, MySQL, SQLite) over ADO.NET. Operations are
/// SQL text with named parameters (<c>@name</c>) bound from the arguments; a query answers its
/// rows as a JSON array, a command <c>{ "rowsAffected": n }</c>. The credential holds a
/// connection string, or a user name and password the settings' host and database complete.
/// The host is checked like an HTTP call's (no private addresses unless the shell allows them);
/// SQLite, a file on the host, only where private networks are allowed.
/// </summary>
public sealed class SqlConnectorProtocol : IConnectorProtocol
{
    public string Id => ConnectorProtocols.Sql;

    public string DisplayName => "SQL database";

    public IReadOnlyList<string> AuthSchemes { get; } = [Connectors.AuthSchemes.Basic, Connectors.AuthSchemes.Custom, Connectors.AuthSchemes.None];

    public IReadOnlyList<string> SchemaFormats => [];

    public IReadOnlyList<ConnectionSettingField> SettingFields { get; } =
    [
        new(SqlConnectionSettings.Provider, "Database", ConnectionSettingFieldTypes.Select, Required: true, Options: SqlProviders.All),
        new(SqlConnectionSettings.Host, "Host", Description: "Leave empty when the connection string names it."),
        new(SqlConnectionSettings.Port, "Port", ConnectionSettingFieldTypes.Number),
        new(SqlConnectionSettings.Database, "Database name"),
        new(SqlConnectionSettings.MaxRows, "Most rows per query", ConnectionSettingFieldTypes.Number, Default: "1000"),
    ];

    public IReadOnlyList<ConnectionSettingField> CredentialFields { get; } =
    [
        new(CredentialKeys.Username, "User name"),
        new(CredentialKeys.Password, "Password", ConnectionSettingFieldTypes.Secret),
        new(CredentialKeys.ConnectionString, "Connection string", ConnectionSettingFieldTypes.Secret, Description: "Instead of, or completing, the fields above."),
    ];

    public Task<SchemaReadResult> ReadOperationsAsync(ConnectionDefinition connection, ConnectionSchemaDocument schema, CancellationToken cancellationToken = default) =>
        Task.FromResult(new SchemaReadResult([], Warnings: ["SQL connections declare their queries as operations."]));

    public async Task<ProtocolResponse> SendAsync(IProtocolCallContext context, CancellationToken cancellationToken = default)
    {
        var connection = context.Connection;
        var provider = connection.Setting(SqlConnectionSettings.Provider);
        var factory = SqlProviders.Factory(provider);
        if (factory is null)
        {
            return Refused($"Connection '{connection.Key}' names no SQL provider ({string.Join(", ", SqlProviders.All)}).");
        }

        var operation = context.Operation;
        var text = context.Request?.Body ?? operation?.Path;
        if (string.IsNullOrWhiteSpace(text))
        {
            return Refused("No SQL to run.");
        }

        var isQuery = operation is not null
            ? operation.Kind != OperationKinds.SqlCommand
            : !string.Equals(context.Request?.Method, SqlConnectionSettings.ExecuteMethod, StringComparison.OrdinalIgnoreCase);

        string connectionString;
        try
        {
            connectionString = await BuildConnectionStringAsync(context, factory, provider!, cancellationToken);
        }
        catch (ConnectorRefusedException ex)
        {
            return Refused(ex.Message);
        }

        await using var db = factory.CreateConnection()!;
        db.ConnectionString = connectionString;
        try
        {
            await db.OpenAsync(cancellationToken);
        }
        catch (Exception ex) when (ex is DbException or InvalidOperationException or ArgumentException)
        {
            // Nothing was sent: the connection never opened.
            var transient = ex is DbException;
            return ProtocolResponse.Failed(new FailureClassification(transient ? FailureKind.Transient : FailureKind.Permanent, NotSent: true), $"Could not open '{connection.Key}': {ex.Message}");
        }

        await using var command = db.CreateCommand();
        command.CommandText = text;
        command.CommandType = CommandType.Text;
        foreach (var (name, value) in context.Arguments ?? [])
        {
            var parameter = command.CreateParameter();
            parameter.ParameterName = name.StartsWith('@') ? name : "@" + name;
            parameter.Value = ToDbValue(value);
            command.Parameters.Add(parameter);
        }

        if (!isQuery)
        {
            var affected = await command.ExecuteNonQueryAsync(cancellationToken);
            var result = new JsonObject { ["rowsAffected"] = affected };
            return new ProtocolResponse { StatusCode = 200, Value = result, Body = result.ToJsonString() };
        }

        var maxRows = int.TryParse(connection.Setting(SqlConnectionSettings.MaxRows), out var max) && max > 0 ? max : 1000;
        var rows = new JsonArray();
        long size = 2;
        await using (var reader = await command.ExecuteReaderAsync(cancellationToken))
        {
            while (rows.Count < maxRows && await reader.ReadAsync(cancellationToken))
            {
                var row = new JsonObject();
                for (var i = 0; i < reader.FieldCount; i++)
                {
                    row[reader.GetName(i)] = reader.IsDBNull(i) ? null : JsonValue.Create(ToJsonValue(reader.GetValue(i)));
                }

                size += row.ToJsonString().Length + 1;
                if (size > context.MaxResponseBytes)
                {
                    throw new ConnectorRefusedException($"The result is larger than {context.MaxResponseBytes} bytes.");
                }

                rows.Add(row);
            }
        }

        return new ProtocolResponse { StatusCode = 200, Value = rows, Body = rows.ToJsonString() };
    }

    public FailureClassification Classify(Exception exception) => exception switch
    {
        ConnectorRefusedException => new FailureClassification(FailureKind.Refused, NotSent: true),
        DbException db when db.IsTransient => new FailureClassification(FailureKind.Transient),
        DbException => new FailureClassification(FailureKind.Permanent),
        TimeoutException or OperationCanceledException => new FailureClassification(FailureKind.Transient),
        _ => new FailureClassification(FailureKind.Permanent),
    };

    private static async Task<string> BuildConnectionStringAsync(IProtocolCallContext context, DbProviderFactory factory, string provider, CancellationToken cancellationToken)
    {
        var connection = context.Connection;
        var builder = factory.CreateConnectionStringBuilder()!;
        if (context.Credential?[CredentialKeys.ConnectionString] is { } fromCredential)
        {
            builder.ConnectionString = fromCredential;
        }

        var hostKey = provider switch
        {
            SqlProviders.PostgreSql => "Host",
            SqlProviders.MySql => "Server",
            _ => "Data Source",
        };
        var databaseKey = provider switch
        {
            SqlProviders.SqlServer => "Initial Catalog",
            SqlProviders.Sqlite => null,
            _ => "Database",
        };

        if (connection.Setting(SqlConnectionSettings.Host) is { } host)
        {
            builder[hostKey] = provider == SqlProviders.SqlServer && connection.Setting(SqlConnectionSettings.Port) is { } sqlPort ? $"{host},{sqlPort}" : host;
            if (provider is SqlProviders.PostgreSql or SqlProviders.MySql && connection.Setting(SqlConnectionSettings.Port) is { } port)
            {
                builder["Port"] = port;
            }
        }

        if (databaseKey is not null && connection.Setting(SqlConnectionSettings.Database) is { } database)
        {
            builder[databaseKey] = database;
        }

        if (context.Credential?[CredentialKeys.Username] is { } user && provider != SqlProviders.Sqlite)
        {
            builder[provider == SqlProviders.SqlServer ? "User ID" : provider == SqlProviders.PostgreSql ? "Username" : "User ID"] = user;
        }

        if (context.Credential?[CredentialKeys.Password] is { } password && provider != SqlProviders.Sqlite)
        {
            builder["Password"] = password;
        }

        if (provider == SqlProviders.Sqlite)
        {
            if (!context.AllowPrivateNetworks)
            {
                throw new ConnectorRefusedException("SQLite databases are files on the host; they are reachable only where the shell allows private networks.");
            }

            return builder.ConnectionString;
        }

        var target = builder.TryGetValue(hostKey, out var value) ? value?.ToString() : null;
        if (string.IsNullOrWhiteSpace(target))
        {
            throw new ConnectorRefusedException($"Connection '{connection.Key}' names no database host.");
        }

        await NetworkGuard.ResolveAsync(HostOf(target, provider), context.AllowPrivateNetworks, cancellationToken);
        return builder.ConnectionString;
    }

    /// <summary>The DNS host of a data source (<c>tcp:host,port</c>, <c>host\instance</c>, <c>host:port</c>, a list).</summary>
    public static string HostOf(string dataSource, string provider)
    {
        var host = dataSource.Trim();
        if (host.StartsWith("tcp:", StringComparison.OrdinalIgnoreCase))
        {
            host = host[4..];
        }

        host = host.Split(',', ';')[0];
        host = host.Split('\\')[0];
        if (provider != SqlProviders.SqlServer && host.Count(c => c == ':') == 1)
        {
            host = host.Split(':')[0];
        }

        return host.Trim().Trim('[', ']');
    }

    private static object ToDbValue(JsonNode? value) => value switch
    {
        null => DBNull.Value,
        JsonValue v when v.TryGetValue<string>(out var s) => s,
        JsonValue v when v.TryGetValue<bool>(out var b) => b,
        JsonValue v when v.TryGetValue<long>(out var l) => l,
        JsonValue v when v.TryGetValue<decimal>(out var d) => d,
        JsonValue v when v.TryGetValue<double>(out var f) => f,
        _ => value.ToJsonString(),
    };

    private static object? ToJsonValue(object value) => value switch
    {
        byte[] bytes => Convert.ToBase64String(bytes),
        DateTime or DateTimeOffset or Guid or string or bool or decimal or double or float or long or int or short or byte => value,
        TimeSpan span => span.ToString(),
        _ => value.ToString(),
    };

    private static ProtocolResponse Refused(string error) =>
        ProtocolResponse.Failed(new FailureClassification(FailureKind.Refused, NotSent: true), error);
}
