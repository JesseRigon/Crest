namespace Crest.Access;

/// <summary>
/// Which shell a request acts in. The side comes from the request (the bucket the shell
/// selector stamped, confirmed by the <c>X-Shell</c> header on API calls), never from the user.
/// </summary>
public enum CallerSide
{
    Site,
    Admin,
    Member,
    /// <summary>A background entry point or a definition published as system.</summary>
    System,
}

/// <summary>
/// Who is asking. Built on the server, once per request (or per operation on a connection
/// that outlives a request, per burst in a workflow), from the authenticated identity plus
/// server-held state. Carries identity and current rights; it is never serialized into an
/// instance, a job or a client.
/// </summary>
public sealed class CallerContext
{
    public required string Tenant { get; init; }

    public required CallerSide Side { get; init; }

    public string? UserId { get; init; }

    public string? UserName { get; init; }

    public bool IsAuthenticated => UserId is not null;

    /// <summary>True for the tenant or organization system actor. Built in process only;
    /// no credential a user can present produces it.</summary>
    public bool IsSystem { get; init; }

    /// <summary>The organization the caller acts in, on the member side or for an
    /// organization system actor; null on the admin and site sides.</summary>
    public string? OrganizationId { get; init; }

    /// <summary>The caller's class (staff, member, ...) as the Members module defines it;
    /// null when no class applies.</summary>
    public string? UserClass { get; init; }

    /// <summary>The user whose session this is when the caller is impersonated.</summary>
    public string? ImpersonatorUserId { get; init; }

    /// <summary>Succeeds every permission: the admin role, the site super user, or a system
    /// actor acting as a tenant admin.</summary>
    public bool IsSuperUser { get; init; }

    public IReadOnlySet<string> Roles { get; init; } = EmptySet;

    /// <summary>Granted permission names, from the caller's roles (and the Anonymous and
    /// Authenticated roles). Implied permissions are resolved by the decision, not stored.</summary>
    public IReadOnlySet<string> Permissions { get; init; } = EmptySet;

    /// <summary>The tenant permission version the rights above were read under.</summary>
    public long PermissionVersion { get; init; }

    public string? Culture { get; init; }

    /// <summary>
    /// The hash of everything that changes a scope: side, organization, class, roles, super
    /// user, and the permission version. Callers with the same signature share compiled
    /// scope plans and cached trees.
    /// </summary>
    public string ScopeSignature => _scopeSignature ??= ComputeScopeSignature();

    private string? _scopeSignature;

    private static readonly IReadOnlySet<string> EmptySet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

    private string ComputeScopeSignature()
    {
        var parts = new List<string>
        {
            UserId ?? string.Empty,
            Side.ToString(),
            OrganizationId ?? string.Empty,
            UserClass ?? string.Empty,
            IsSuperUser ? "super" : string.Empty,
            IsSystem ? "system" : string.Empty,
            PermissionVersion.ToString(System.Globalization.CultureInfo.InvariantCulture),
        };
        parts.AddRange(Roles.OrderBy(role => role, StringComparer.OrdinalIgnoreCase));
        var bytes = System.Text.Encoding.UTF8.GetBytes(string.Join('\u001f', parts));
        return Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(bytes))[..32];
    }

    public static CallerContext Anonymous(string tenant, CallerSide side, IReadOnlySet<string>? permissions = null, long permissionVersion = 0) => new()
    {
        Tenant = tenant,
        Side = side,
        Permissions = permissions ?? EmptySet,
        PermissionVersion = permissionVersion,
    };
}

/// <summary>
/// The caller of the current request, operation or burst. Set by the access gate; read by
/// the decision, the scope and every pipeline. Null outside a gated execution.
/// </summary>
public interface ICallerContextAccessor
{
    CallerContext? Current { get; set; }
}
