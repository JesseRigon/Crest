using Grove.Access;
using Grove.Contents.Management;
using Grove.Access.Users;
using Grove.Access.Users.Indexes;
using Grove.Access.Users.Models;
using Grove.Infrastructure.Settings;
using YesSql;

namespace Grove.Services;

/// <summary>
/// Exposes users as a picker source, with as many columns as the dropdown asks for.
/// </summary>
/// <remarks>
/// Every user reference is an option list field on this source (docs/content-engine.md ›
/// Picking is one field type): the user name by default, more columns where a field asks for
/// them ("show the rep's name AND badge number"), limited to some roles by a filter on
/// <see cref="Columns.Roles"/>.
///
/// Custom per-user values (a badge number, a department) live in the user's own
/// content properties, which is why <see cref="Columns.PropertyPrefix"/> exists as a
/// prefix: "Property:Badge" reads Badge from the user's content, so tenants can
/// display whatever their user profile carries without this provider knowing about
/// it.
/// </remarks>
/// <para>
/// Access: listing users takes <c>ListUsers</c> for the caller; the columns beyond the user
/// name (email, phone, profile properties) take <c>ViewUsers</c>. A user referenced from a
/// document the caller may see keeps its name either way.
/// </para>
public sealed class UserOptionSourceProvider(ISession session, ICallerContextAccessor callers, IAccessDecision decision) : IOptionSourceProvider
{
    /// <summary>Source key for users; the qualifier is unused.</summary>
    public const string ProviderKey = Grove.Models.OptionSourceKeys.UserProvider;

    public string Key => ProviderKey;

    public static class Columns
    {
        public const string UserName = "UserName";
        public const string Email = "Email";
        public const string PhoneNumber = "PhoneNumber";
        public const string IsEnabled = "IsEnabled";

        /// <summary>The user's roles, comma-separated. A filter on it (Equals or In) keeps the
        /// users holding any of the named roles (a field limited to some roles).</summary>
        public const string Roles = "Roles";

        /// <summary>Prefix for values read from the user's content properties, e.g.
        /// "Property:Badge" or "Property:UserProfile.Department".</summary>
        public const string PropertyPrefix = "Property:";
    }

    public Task<IReadOnlyList<OptionSourceColumnDescriptor>> DescribeColumnsAsync(string qualifier, CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<OptionSourceColumnDescriptor>>(
        [
            new(Columns.UserName, "User name", IsDefaultDisplay: true),
            new(Columns.Email, "Email"),
            new(Columns.PhoneNumber, "Phone number"),
            new(Columns.IsEnabled, "Enabled"),
            new(Columns.Roles, "Roles"),
        ]);

    public async Task<IReadOnlyList<OptionRow>> QueryAsync(OptionSourceQuery query, CancellationToken cancellationToken = default)
    {
        if (!await decision.AllowsAsync(callers.Current, UsersPermissions.ListUsers.Name, cancellationToken: cancellationToken))
        {
            return [];
        }

        var columns = await VisibleColumnsAsync(query.Columns, cancellationToken);
        var (roles, filters) = SplitRoleFilters(query.Filters);
        var users = (await session.Query<User, UserIndex>().ListAsync())
            .Where(user => roles is null || user.RoleNames.Any(role => roles.Contains(role)));
        var rows = users.Select(user => ToRow(user, columns));

        rows = OptionFilterMatcher.Apply(rows, filters);
        rows = OptionFilterMatcher.Search(rows, query.SearchText, query.SearchColumns is { Count: > 0 }
            ? query.SearchColumns
            : [Columns.UserName, Columns.Email]);

        // Instance sort when requested; the provider's natural user-name order
        // otherwise. The whole set is materialized above, so sorting precedes paging.
        rows = query.SortColumns is { Count: > 0 }
            ? OptionRowSorter.Apply(rows, query.SortColumns)
            : rows.OrderBy(row => row.Values.TryGetValue(Columns.UserName, out var name) ? name : row.Id, StringComparer.OrdinalIgnoreCase);

        return [.. rows
            .Skip(query.Skip)
            .Take(query.Take)];
    }

    public async Task<IReadOnlyList<OptionRow>> GetByIdsAsync(string qualifier, IReadOnlyList<string> ids, IReadOnlyList<string> columns, CancellationToken cancellationToken = default)
    {
        if (ids.Count == 0)
        {
            return [];
        }

        // One query for the whole id set - rendering a document full of user
        // references must not become one lookup per line. (Filtering happens after
        // materializing, the same way UsersController queries UserIndex.)
        var wanted = new HashSet<string>(ids, StringComparer.OrdinalIgnoreCase);
        var users = await session.Query<User, UserIndex>().ListAsync();

        var visible = await VisibleColumnsAsync(columns, cancellationToken);
        return [.. users.Where(user => wanted.Contains(user.UserId)).Select(user => ToRow(user, visible))];
    }

    // A role filter keeps the users holding any of its roles: the roles column is a list, which
    // the generic matcher compares as one string.
    private static (HashSet<string>? Roles, IReadOnlyList<ResolvedOptionFilter>? Others) SplitRoleFilters(IReadOnlyList<ResolvedOptionFilter>? filters)
    {
        if (filters is null || !filters.Any(IsRoleFilter))
        {
            return (null, filters);
        }

        var roles = filters.Where(IsRoleFilter).SelectMany(filter => filter.Values)
            .SelectMany(value => (value ?? string.Empty).Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        return (roles, [.. filters.Where(filter => !IsRoleFilter(filter))]);
    }

    private static bool IsRoleFilter(ResolvedOptionFilter filter) =>
        string.Equals(filter.Path, Columns.Roles, StringComparison.OrdinalIgnoreCase)
        && filter.Operator is OptionFilterOperators.Equals or OptionFilterOperators.In or OptionFilterOperators.Contains;

    /// <summary>The requested columns the caller may read: the user name always, the rest with ViewUsers.</summary>
    private async Task<IReadOnlyList<string>> VisibleColumnsAsync(IReadOnlyList<string> requested, CancellationToken cancellationToken)
    {
        if (requested is not { Count: > 0 } || requested.All(column => column == Columns.UserName) ||
            await decision.AllowsAsync(callers.Current, UsersPermissions.ViewUsers.Name, cancellationToken: cancellationToken))
        {
            return requested;
        }

        return [Columns.UserName];
    }

    // Internal so the multi-column projection can be unit-tested directly - it is the
    // behaviour that justifies this provider existing.
    internal static OptionRow ToRow(User user, IReadOnlyList<string> columns)
    {
        var requested = columns is { Count: > 0 } ? columns : [Columns.UserName];
        var values = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);

        foreach (var column in requested)
        {
            values[column] = column switch
            {
                Columns.UserName => user.UserName,
                Columns.Email => user.Email,
                Columns.PhoneNumber => user.PhoneNumber,
                Columns.IsEnabled => user.IsEnabled ? "true" : "false",
                Columns.Roles => string.Join(", ", user.RoleNames),
                _ when column.StartsWith(Columns.PropertyPrefix, StringComparison.OrdinalIgnoreCase) =>
                    ReadProperty(user, column[Columns.PropertyPrefix.Length..]),
                _ => null,
            };
        }

        // A user's identity IS its id, so Key stays null - which is exactly what the
        // index records for entity sources.
        return new OptionRow(user.UserId, values);
    }

    // Walks a dotted path through the user's content JSON, so a tenant can display
    // any value their user profile carries.
    private static string? ReadProperty(User user, string path)
    {
        System.Text.Json.Nodes.JsonNode? node = user.Properties;
        foreach (var segment in path.Split('.', StringSplitOptions.RemoveEmptyEntries))
        {
            if (node is null)
            {
                return null;
            }

            node = node[segment];
        }

        return node?.ToString();
    }
}
