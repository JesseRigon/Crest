using Grove.Access;
using Grove.Access.Roles;
using Grove.Access.Users.Indexes;
using Grove.Access.Users.Models;
using YesSql;
using YesSql.Services;

namespace Grove.Access.Sharing.Services;

/// <summary>
/// Sharing's own principals for pickers and names (docs/sharing.md § 8, requirement 46):
/// Anyone, the tenant, every role of both kinds from the role registry (derived roles labelled),
/// and, for staff-side callers, enabled users by name. The Anonymous and Authenticated roles are
/// Anyone and the tenant, so they are not offered as roles. System accounts are not users, so
/// they are never offered (requirement 34).
/// </summary>
public sealed class SharingPrincipalDirectory(
    ICallerContextAccessor callers,
    IRoleRegistry roles,
    ISession session) : ISharePrincipalDirectory
{
    private static readonly HashSet<string> s_hidden = new(StringComparer.OrdinalIgnoreCase) { PlatformConstants.Roles.Anonymous, PlatformConstants.Roles.Authenticated };

    public async Task<IReadOnlyList<SharePrincipalOption>> SearchAsync(string? text, int max, CancellationToken cancellationToken = default)
    {
        var options = new List<SharePrincipalOption>
        {
            new(SharePrincipal.Anyone, "Anyone", "Everyone, signed in or not: public"),
            new(SharePrincipal.Tenant, "Everyone signed in", "Everyone signed in to this site, staff and members"),
        };

        foreach (var role in await roles.ListAsync(cancellationToken))
        {
            if (!s_hidden.Contains(role.RoleName))
            {
                options.Add(new SharePrincipalOption(
                    SharePrincipal.Role(role.RoleName),
                    role.RoleName,
                    role.Kind == RoleKind.Derived ? $"Derived role ({role.Provider}): {role.Description}" : role.Description,
                    role.Kind == RoleKind.Derived));
            }
        }

        var matching = options.Where(option => Matches(option.DisplayName, text)).ToList();

        // People are offered to staff only: a member does not browse the tenant's users.
        if (callers.Current is { } caller && (caller.IsSuperUser || caller.Side == CallerSide.Admin) && matching.Count < max)
        {
            var normalized = (text ?? string.Empty).Trim().ToUpperInvariant();
            var query = normalized.Length == 0
                ? session.Query<User, UserIndex>(index => index.IsEnabled)
                : session.Query<User, UserIndex>(index => index.IsEnabled && (index.NormalizedUserName.Contains(normalized) || index.NormalizedEmail.Contains(normalized)));
            foreach (var user in await query.OrderBy(index => index.NormalizedUserName).Take(max - matching.Count).ListAsync(cancellationToken))
            {
                if (!SystemAccounts.IsSystemAccount(user.UserId))
                {
                    matching.Add(new SharePrincipalOption(SharePrincipal.User(user.UserId), user.UserName, user.Email));
                }
            }
        }

        return [.. matching.Take(max)];
    }

    public async Task<IReadOnlyDictionary<SharePrincipal, string>> NamesAsync(IReadOnlyCollection<SharePrincipal> principals, CancellationToken cancellationToken = default)
    {
        var names = new Dictionary<SharePrincipal, string>();
        foreach (var principal in principals)
        {
            switch (principal.Kind)
            {
                case SharePrincipalKind.Anyone:
                    names[principal] = "Anyone";
                    break;
                case SharePrincipalKind.Tenant:
                    names[principal] = "Everyone signed in";
                    break;
                case SharePrincipalKind.Role when principal.Id is { } role:
                    names[principal] = principal.OrganizationId is null ? role : $"{role} ({principal.OrganizationId})";
                    break;
                case SharePrincipalKind.User when SystemAccounts.IsSystemAccount(principal.Id):
                    names[principal] = SystemAccounts.DisplayName(principal.Id!);
                    break;
            }
        }

        var userIds = principals.Where(principal => principal.Kind == SharePrincipalKind.User && principal.Id is not null && !SystemAccounts.IsSystemAccount(principal.Id)).Select(principal => principal.Id!).Distinct().ToArray();
        if (userIds.Length > 0)
        {
            foreach (var user in await session.Query<User, UserIndex>(index => index.UserId.IsIn(userIds)).ListAsync(cancellationToken))
            {
                names[SharePrincipal.User(user.UserId)] = user.UserName;
            }
        }

        return names;
    }

    private static bool Matches(string name, string? text) =>
        string.IsNullOrWhiteSpace(text) || name.Contains(text.Trim(), StringComparison.OrdinalIgnoreCase);
}
