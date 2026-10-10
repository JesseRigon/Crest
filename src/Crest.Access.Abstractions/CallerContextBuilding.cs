using System.Security.Claims;

namespace Crest.Access;

/// <summary>What the gate knows about a request before the caller is built.</summary>
public sealed record CallerRequest(
    ClaimsPrincipal? Principal,
    CallerSide Side,
    /// <summary>The organization the request names (the <c>X-Org</c> header or the member
    /// path); validated against the caller's bindings by the Members contributor.</summary>
    string? RequestedOrganizationId,
    string? Culture,
    /// <summary>The staff user impersonating this caller, when the session says so; read
    /// per request by a <see cref="ICallerRequestResolver"/>, never cached.</summary>
    string? ImpersonatorUserId = null);

/// <summary>
/// Completes what a request names before the caller is built and before the cache is
/// consulted: the Members module fills the organization from the session when the request
/// names none, and the impersonator. Request-level facts live here so the cached caller
/// state never carries them.
/// </summary>
public interface ICallerRequestResolver
{
    Task<CallerRequest> ResolveAsync(CallerRequest request, CancellationToken cancellationToken = default);
}

/// <summary>The well-known user classes a caller can carry.</summary>
public static class CallerClasses
{
    public const string Staff = "staff";

    public const string Member = "member";
}

/// <summary>The caller under construction. Contributors add what their module knows.</summary>
public sealed class CallerContextBuilder
{
    public CallerContextBuilder(CallerRequest request, string tenant)
    {
        Request = request;
        Tenant = tenant;
    }

    public CallerRequest Request { get; }

    public string Tenant { get; }

    public string? UserId { get; set; }

    public string? UserName { get; set; }

    public string? OrganizationId { get; set; }

    public string? UserClass { get; set; }

    public string? ImpersonatorUserId { get; set; }

    public bool IsSuperUser { get; set; }

    public HashSet<string> Roles { get; } = new(StringComparer.OrdinalIgnoreCase);

    public HashSet<string> Permissions { get; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Set by a contributor to refuse the request outright (a member-side request
    /// for an organization the user holds no binding to).</summary>
    public string? DenialReason { get; set; }
}

/// <summary>
/// Adds a module's knowledge to the caller: the Members module contributes the organization,
/// the binding's roles and the user class; a policy module narrows the caller. Runs after
/// the core builder resolved identity, roles and permissions.
/// </summary>
public interface ICallerContextContributor
{
    Task ContributeAsync(CallerContextBuilder builder, CancellationToken cancellationToken = default);
}

/// <summary>Builds the caller for a request, from the identity plus server-held state.</summary>
public interface ICallerContextFactory
{
    Task<CallerContext> CreateAsync(CallerRequest request, CancellationToken cancellationToken = default);

    /// <summary>The system actor, built in process only: with no organization it acts as a
    /// tenant admin; with one it acts as that organization's admin.</summary>
    Task<CallerContext> CreateSystemAsync(string? organizationId = null, CancellationToken cancellationToken = default);
}

/// <summary>Runs work as a given caller: background entry points, system flows, tests.</summary>
public interface IAccessRunner
{
    Task RunAsAsync(CallerContext caller, Func<Task> work);

    Task<T> RunAsAsync<T>(CallerContext caller, Func<Task<T>> work);
}

public static class AccessHeaders
{
    public const string Shell = "X-Shell";

    public const string Organization = "X-Org";
}

/// <summary>Thrown by the caller factory when a contributor refuses the request (a member-side
/// request for an organization the user holds no binding to, markers that disagree with the
/// bucket). The gate answers 403.</summary>
public sealed class CallerDeniedException(string reason) : UnauthorizedAccessException(reason)
{
}
