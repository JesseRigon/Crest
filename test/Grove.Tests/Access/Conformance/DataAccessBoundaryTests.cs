using System.Text.RegularExpressions;

namespace Grove.Tests.Access.Conformance;

/// <summary>
/// The boundary the analyzer in Grove.Infrastructure.Build will enforce (docs/workflows.md › Tasks › 1, the
/// analyzer), checked over the source until it exists: raw connections and Dapper only in
/// the data layer, the query sources and migrations; no surface that answers a caller
/// (controllers, endpoints, GraphQL, Liquid) queries the session itself - reads go through
/// the query pipeline, which conjoins the caller's scope. Each failure names the file.
/// These fail today on the bypasses listed in docs/queries.md; they are the target.
/// </summary>
public partial class DataAccessBoundaryTests
{
    private static readonly string Root = FindRoot();

    private static string FindRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Grove.slnx")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? throw new InvalidOperationException("The repository root (Grove.slnx) was not found.");
    }

    /// <summary>Production C# under src/, without build output, tests, the vendored engine and designer.</summary>
    private static IEnumerable<string> SourceFiles() =>
        Directory.EnumerateFiles(Path.Combine(Root, "src"), "*.cs", SearchOption.AllDirectories)
            .Select(path => Path.GetRelativePath(Root, path).Replace('\\', '/'))
            .Where(path =>
                !path.Contains("/obj/", StringComparison.Ordinal) &&
                !path.Contains("/bin/", StringComparison.Ordinal) &&
                !path.Contains("/tests/", StringComparison.Ordinal) &&
                !path.Contains("/engine/", StringComparison.Ordinal) &&
                !path.Contains("/designer/", StringComparison.Ordinal) &&
                !path.Contains("/reference/", StringComparison.Ordinal));

    private static bool IsMigration(string path) =>
        path.Contains("Migration", StringComparison.Ordinal);

    // Where a raw connection is the data layer's own business.
    private static readonly string[] RawConnectionOwners =
    [
        "src/Grove.Data/",
        "src/Grove.Data.YesSql/",
        "src/Grove.Data.Abstractions/",
        "src/Grove.Data.YesSql.Abstractions/",
        "src/Grove.Infrastructure.Setup/",
        "src/Grove.Diagnostics.MiniProfiler/",
        "src/Grove.Data.Queries/Sql/SqlQuerySource.cs",
        "src/Grove.Data.Queries/Structured/StructuredQuerySource.cs",
        "src/Grove.Data.Queries.Abstractions/",
    ];

    [Fact]
    public void RawConnectionsAndDapperStayInTheDataLayer()
    {
        var offenders = SourceFiles()
            .Where(path => !IsMigration(path) && !RawConnectionOwners.Any(owner => path.StartsWith(owner, StringComparison.Ordinal)))
            .Where(path => RawAccess().IsMatch(File.ReadAllText(Path.Combine(Root, path))))
            .ToArray();

        Assert.True(offenders.Length == 0, "Raw database access outside the data layer:\n" + string.Join('\n', offenders));
    }

    [Fact]
    public void NoSurfaceQueriesTheSessionItself()
    {
        var offenders = SourceFiles()
            .Where(IsSurface)
            .Where(path => SessionQuery().IsMatch(File.ReadAllText(Path.Combine(Root, path))))
            .ToArray();

        Assert.True(offenders.Length == 0, "Surfaces reading through ISession instead of the query pipeline:\n" + string.Join('\n', offenders));
    }

    [Fact]
    public void NoSecondStoreIsBuilt()
    {
        // One YesSql store per tenant, built by Grove.Data.YesSql. A module configuring its
        // own store is a second persistence system.
        var offenders = SourceFiles()
            .Where(path => !path.StartsWith("src/Grove.Data.YesSql/", StringComparison.Ordinal))
            .Where(path => StoreBuilt().IsMatch(File.ReadAllText(Path.Combine(Root, path))))
            .ToArray();

        Assert.True(offenders.Length == 0, "Stores built outside Grove.Data.YesSql:\n" + string.Join('\n', offenders));
    }

    [Fact]
    public void NoSessionIsOpenedBesideTheUnitOfWork()
    {
        // A session created ad hoc commits on its own, outside the request's or the burst's
        // unit of work.
        var offenders = SourceFiles()
            .Where(path => !path.StartsWith("src/Grove.Data.YesSql/", StringComparison.Ordinal) && !path.StartsWith("src/Grove.Data/", StringComparison.Ordinal) && !IsMigration(path))
            .Where(path => AdHocSession().IsMatch(File.ReadAllText(Path.Combine(Root, path))))
            .ToArray();

        Assert.True(offenders.Length == 0, "Sessions opened outside the unit of work:\n" + string.Join('\n', offenders));
    }

    private static bool IsSurface(string path) =>
        path.Contains("/Controllers/", StringComparison.Ordinal) ||
        path.Contains("/Endpoints/", StringComparison.Ordinal) ||
        path.Contains("/GraphQL/", StringComparison.Ordinal) ||
        path.Contains("/Liquid/", StringComparison.Ordinal) ||
        path.EndsWith("Controller.cs", StringComparison.Ordinal);

    [GeneratedRegex(@"\bIDbConnectionAccessor\b|\busing\s+Dapper\s*;|\bnew\s+(Sql|Npgsql|Sqlite|MySql)Connection\s*\(|\.ConnectionFactory\.CreateConnection\s*\(")]
    private static partial Regex RawAccess();

    [GeneratedRegex(@"\b_?session\s*\.\s*(Query|QueryIndex)\s*<|\bISession\b[^;\n]*\)\s*\.\s*Query\s*<")]
    private static partial Regex SessionQuery();

    [GeneratedRegex(@"new\s+YesSql\.Configuration\s*\(|\bStoreFactory\.CreateAndInitializeAsync\s*\(")]
    private static partial Regex StoreBuilt();

    [GeneratedRegex(@"\b(store|_store|Store)\s*\.\s*CreateSession\s*\(")]
    private static partial Regex AdHocSession();
}
