namespace Grove.Access;

/// <summary>
/// System accounts (docs/sharing.md § 1, requirements 33-36): principals that own things rather
/// than people. Each tenant has one tenant account and each organization one organization
/// account, existing with the tenant or organization they belong to. They are ids, not user
/// records, so by construction they cannot sign in or be impersonated (there is no credential or
/// session to take over), appear in no user list or picker, and are never counted as a user,
/// member or seat. The tenant and organization system actors (<see cref="CallerContext.IsSystem"/>)
/// act as them, and what they do is attributed to the account in the audit trail
/// (<see cref="CallerContext.ActorId"/>), distinct from people and from machine actors.
/// </summary>
public static class SystemAccounts
{
    /// <summary>Every system account id starts with it; no user id may.</summary>
    public const string Prefix = "system:";

    /// <summary>The tenant's account: owns what the tenant owns (shared drives).</summary>
    public const string Tenant = Prefix + "tenant";

    private const string OrganizationPrefix = Prefix + "organization:";

    /// <summary>An organization's account: owns what the organization owns (its drive).</summary>
    public static string Organization(string organizationId) => OrganizationPrefix + organizationId;

    /// <summary>The tenant's account, or the organization's when one is given.</summary>
    public static string Of(string? organizationId) => organizationId is null ? Tenant : Organization(organizationId);

    public static bool IsSystemAccount(string? id) => id is not null && id.StartsWith(Prefix, StringComparison.Ordinal);

    /// <summary>The organization an organization account belongs to; null for any other id.</summary>
    public static string? OrganizationOf(string? id) =>
        id is not null && id.StartsWith(OrganizationPrefix, StringComparison.Ordinal) ? id[OrganizationPrefix.Length..] : null;

    /// <summary>How the audit trail and screens name the account.</summary>
    public static string DisplayName(string id) =>
        id == Tenant ? "Tenant account" : OrganizationOf(id) is { } organizationId ? $"Organization account ({organizationId})" : id;
}
